using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    public class ServiceBusCloudEventMessage : Message
    {
        public ServiceBusCloudEventMessage(CloudEvent cloudEvent, ICloudEventFormatter formatter)
        {
            if (cloudEvent == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent));
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            Body = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
            ContentType = contentType.MediaType;
            MessageId = cloudEvent.Id;
            MapHeaders(cloudEvent);
        }

        public ServiceBusCloudEventMessage(CloudEvent cloudEvent)
        {
            if (cloudEvent == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent));
            }

            switch (cloudEvent.Data)
            {
                case byte[] bytes:
                    Body = bytes;
                    break;
                case MemoryStream stream:
                    Body = stream.ToArray();
                    break;
                case Stream stream:
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        Body = buffer.ToArray();
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported data type: {cloudEvent.Data.GetType().FullName}");
            }

            ContentType = cloudEvent.DataContentType?.MediaType;

            MessageId = cloudEvent.Id;
            MapHeaders(cloudEvent);
        }

        private void MapHeaders(CloudEvent cloudEvent)
        {
            var ignoreKeys = new List<string>(3)
                {
                    CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion),
                    CloudEventAttributes.IdAttributeName(cloudEvent.SpecVersion),
                    CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion),
                };

            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (!ignoreKeys.Contains(attribute.Key))
                {
                    var key = Constants.PropertyKeyPrefix + attribute.Key;
                    switch (attribute.Value)
                    {
                        case Uri uri:
                            UserProperties.Add(key, uri.ToString());
                            break;
                        default:
                            UserProperties.Add(key, attribute.Value);
                            break;
                    }
                }
            }
        }
    }
}
