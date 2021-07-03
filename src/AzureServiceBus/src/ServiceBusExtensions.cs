using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;

using CloudNative.CloudEvents.Core;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    public static class ServiceBusExtensions
    {
        public static bool IsCloudEvent(this Message message)
        {
            Validation.CheckNotNull(message, nameof(message));
            return HasCloudEventsContentType(message, out _) ||
                message.UserProperties.ContainsKey(Constants.SpecVersionPropertyKey);
        }

        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            params CloudEventAttribute[] extensionAttributes
            )
            => message.ToCloudEvent(formatter, (IEnumerable<CloudEventAttribute>)extensionAttributes);

        public static CloudEvent ToCloudEvent(
            this Message message,
            CloudEventFormatter formatter,
            IEnumerable<CloudEventAttribute> extensionAttributes
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
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => pair.Value,
                };

                properties.Add(propertyKey, propertyValue);
            }
        }
    }
}
