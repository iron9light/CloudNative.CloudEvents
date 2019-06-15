using System;
using System.IO;
using System.Net.Mime;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    public static class MessageExtensions
    {
        private static JsonEventFormatter _jsonFormatter = new JsonEventFormatter();

        public static bool IsCloudEvent(this Message message)
        {
            return (message.ContentType != null && message.ContentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase)) ||
                message.UserProperties.ContainsKey(Constants.SpecVersion2PropertyKey) ||
                message.UserProperties.ContainsKey(Constants.SpecVersion1PropertyKey);
        }

        public static CloudEvent ToCloudEvent(this Message message, params ICloudEventExtension[] extensions)
        {
            return InternalToCloudEvent(message, null, extensions);
        }

        public static CloudEvent ToCloudEvent(this Message message, ICloudEventFormatter formatter, params ICloudEventExtension[] extensions)
        {
            return InternalToCloudEvent(message, formatter, extensions);
        }

        private static CloudEvent InternalToCloudEvent(Message message, ICloudEventFormatter? formatter, ICloudEventExtension[] extensions)
        {
            var contentType = message.ContentType;
            if (contentType != null && contentType.StartsWith(CloudEvent.MediaType, StringComparison.InvariantCultureIgnoreCase))
            {
                return StructuredToCloudEvent(message, formatter, extensions);
            }
            else
            {
                return BinaryToCloudEvent(message, extensions);
            }
        }

        private static CloudEvent StructuredToCloudEvent(Message message, ICloudEventFormatter? formatter, ICloudEventExtension[] extensions)
        {
            if (formatter == null)
            {
                if (message.ContentType.EndsWith(JsonEventFormatter.MediaTypeSuffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    formatter = _jsonFormatter;
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported CloudEvents ContentType: {message.ContentType}");
                }
            }

            using (var stream = new MemoryStream(message.Body))
            {
                return formatter.DecodeStructuredEvent(stream, extensions);
            }
        }

        private static CloudEvent BinaryToCloudEvent(Message message, ICloudEventExtension[] extensions)
        {
            var specVersion = GetCloudEventsSpecVersion(message);
            var cloudEventType = GetAttribute(message, CloudEventAttributes.TypeAttributeName(specVersion));
            var cloudEventSource = new Uri(GetAttribute(message, CloudEventAttributes.SourceAttributeName(specVersion)));
            var cloudEvent = new CloudEvent(specVersion, cloudEventType, cloudEventSource, id: message.MessageId, extensions: extensions);
            var attributes = cloudEvent.GetAttributes();
            foreach (var property in message.UserProperties)
            {
                if (property.Key.StartsWith(Constants.PropertyKeyPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    var key = property.Key.Substring(Constants.PropertyKeyPrefix.Length).ToLowerInvariant();

                    attributes[key] = property.Value;
                }
            }

            cloudEvent.DataContentType = message.ContentType == null ? null : new ContentType(message.ContentType);
            cloudEvent.Data = message.Body;
            return cloudEvent;
        }

        private static CloudEventsSpecVersion GetCloudEventsSpecVersion(Message message)
        {
            if (message.UserProperties.ContainsKey(Constants.SpecVersion1PropertyKey))
            {
                return CloudEventsSpecVersion.V0_1;
            }
            else if (message.UserProperties.ContainsKey(Constants.SpecVersion2PropertyKey))
            {
                switch (message.UserProperties[Constants.SpecVersion2PropertyKey])
                {
                    case "0.2":
                        return CloudEventsSpecVersion.V0_2;
                    case "0.3":
                        return CloudEventsSpecVersion.V0_3;
                }
            }

            return CloudEventsSpecVersion.Default;
        }

        private static string? GetAttribute(Message message, string key)
        {
            var propertyKey = Constants.PropertyKeyPrefix + key;
            if (message.UserProperties.TryGetValue(propertyKey, out var value))
            {
                return value.ToString();
            }
            else
            {
                return null;
            }
        }
    }
}
