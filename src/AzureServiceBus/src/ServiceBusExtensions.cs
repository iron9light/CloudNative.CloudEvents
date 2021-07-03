using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;

using CloudNative.CloudEvents.Core;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    /// <summary>
    /// Extension methods to convert between CloudEvents and ServiceBus messages.
    /// </summary>
    public static class ServiceBusExtensions
    {
        /// <summary>
        /// Indicates whether this <see cref="Message"/> holds a single CloudEvent.
        /// </summary>
        /// <remarks>
        /// This method returns false for batch requests, as they need to be parsed differently.
        /// </remarks>
        /// <param name="message">The message to check for the presence of a CloudEvent. Must not be null.</param>
        /// <returns>true, if the message is a CloudEvent.</returns>
        public static bool IsCloudEvent(this Message message)
        {
            Validation.CheckNotNull(message, nameof(message));
            return HasCloudEventsContentType(message, out _) ||
                message.UserProperties.ContainsKey(Constants.SpecVersionPropertyKey);
        }

        /// <summary>
        /// Converts this <see cref="Message"/> into a CloudEvent object.
        /// </summary>
        /// <param name="message">The message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent.</param>
        /// <returns>A validated CloudEvent.</returns>
        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes
            )
            => message.ToCloudEvent(formatter, (IEnumerable<CloudEventAttribute>)extensionAttributes);

        /// <summary>
        /// Converts this <see cref="Message"/> into a CloudEvent object.
        /// </summary>
        /// <param name="message">The message to convert. Must not be null.</param>
        /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
        /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
        /// <returns>A validated CloudEvent.</returns>
        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute>? extensionAttributes
            )
        {
            Validation.CheckNotNull(message, nameof(message));
            Validation.CheckNotNull(formatter, nameof(formatter));
            if (HasCloudEventsContentType(message, out var contentType))
            {
                using (var stream = new MemoryStream(message.Body))
                {
                    var cloudEvent = formatter.DecodeStructuredModeMessage(stream, new ContentType(contentType), extensionAttributes);
                    cloudEvent.Id = message.MessageId;
                    return cloudEvent;
                }
            }
            else
            {
                var propertyMap = message.UserProperties;
                if (!propertyMap.TryGetValue(Constants.SpecVersionPropertyKey, out var versionId))
                {
                    throw new ArgumentException("Request is not a CloudEvent", nameof(message));
                }

                var version = CloudEventsSpecVersion.FromVersionId(versionId as string)
                    ?? throw new ArgumentException($"Unknown CloudEvents spec version '{versionId}'", nameof(message));

                var cloudEvent = new CloudEvent(version, extensionAttributes)
                {
                    Id = message.MessageId,
                    DataContentType = message.ContentType,
                };

                foreach (var property in propertyMap)
                {
                    if (!property.Key.StartsWith(Constants.PropertyKeyPrefix))
                    {
                        continue;
                    }

                    var attributeName = property.Key.Substring(Constants.PropertyKeyPrefix.Length).ToLowerInvariant();

                    // We've already dealt with the spec version.
                    if (attributeName == CloudEventsSpecVersion.SpecVersionAttribute.Name)
                    {
                        continue;
                    }

                    // Timestamps are serialized via DateTime instead of DateTimeOffset.
                    if (property.Value is DateTime dt)
                    {
                        if (dt.Kind != DateTimeKind.Utc)
                        {
                            // This should only happen for MinValue and MaxValue...
                            // just respecify as UTC. (We could add validation that it really
                            // *is* MinValue or MaxValue if we wanted to.)
                            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        }

                        cloudEvent[attributeName] = (DateTimeOffset)dt;
                    }

                    // URIs are serialized as strings, but we need to convert them back to URIs.
                    // It's simplest to let CloudEvent do this for us.
                    else if (property.Value is string text)
                    {
                        cloudEvent.SetAttributeFromString(attributeName, text);
                    }
                    else
                    {
                        cloudEvent[attributeName] = property.Value;
                    }
                }

                formatter.DecodeBinaryModeEventData(message.Body, cloudEvent);

                Validation.CheckCloudEventArgument(cloudEvent, nameof(message));

                return cloudEvent;
            }
        }

        private static bool HasCloudEventsContentType(Message message, out string contentType)
        {
            contentType = message.ContentType;
            return MimeUtilities.IsCloudEventsContentType(contentType);
        }

        /// <summary>
        /// Converts a CloudEvent to <see cref="Message"/>.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
        /// <param name="contentMode">Content mode. Structured or binary.</param>
        /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
        /// <returns>A <see cref="Message"/>.</returns>
        public static Message ToServiceBusMessage(
            this CloudEvent cloudEvent,
            ContentMode contentMode,
            CloudEventFormatter formatter
            )
        {
            Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
            Validation.CheckNotNull(formatter, nameof(formatter));

            Message message;

            switch (contentMode)
            {
                case ContentMode.Structured:
                    message = new Message(
                        BinaryDataUtilities.AsArray(formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType))
                        )
                    {
                        ContentType = contentType.MediaType,
                    };
                    break;
                case ContentMode.Binary:
                    message = new Message(
                        BinaryDataUtilities.AsArray(formatter.EncodeBinaryModeEventData(cloudEvent))
                        )
                    {
                        ContentType = cloudEvent.DataContentType,
                    };
                    break;
                default:
                    throw new ArgumentException($"Unsupported content mode: {contentMode}", nameof(contentMode));
            }

            MapHeaders(cloudEvent, message);

            return message;
        }

        private static void MapHeaders(CloudEvent cloudEvent, Message message)
        {
            message.MessageId = cloudEvent.Id;
            var properties = message.UserProperties;
            properties.Add(Constants.SpecVersionPropertyKey, cloudEvent.SpecVersion.VersionId);

            foreach (var pair in cloudEvent.GetPopulatedAttributes())
            {
                var attribute = pair.Key;

                // The content type and id is specified elsewhere.
                if (attribute == cloudEvent.SpecVersion.DataContentTypeAttribute || attribute == cloudEvent.SpecVersion.IdAttribute)
                {
                    continue;
                }

                string propertyKey = Constants.PropertyKeyPrefix + attribute.Name;

                var propertyValue = pair.Value switch
                {
                    Uri uri => uri.ToString(),

                    // Compat with CloudEvents.Amqp implemenation
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => pair.Value,
                };

                properties.Add(propertyKey, propertyValue);
            }
        }
    }
}
