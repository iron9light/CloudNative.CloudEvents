using System;
using System.IO;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    public class CloudEventMessage : Message
    {
        public CloudEventMessage(CloudEvent cloudEvent, ICloudEventFormatter formatter)
        {
            Body = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
            ContentType = contentType.MediaType;
            MessageId = cloudEvent.Id;
            MapHeaders(cloudEvent);
        }

        public CloudEventMessage(CloudEvent cloudEvent)
        {
            switch (cloudEvent.Data)
            {
                case byte[] bytes:
                    Body = bytes;
                    break;
                case MemoryStream stream:
                    Body = stream.ToArray();
                    break;
                case Stream stream:
                    var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    Body = buffer.ToArray();
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
            foreach (var attribute in cloudEvent.GetAttributes())
            {
                if (attribute.Key != CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion) &&
                    attribute.Key != CloudEventAttributes.IdAttributeName(cloudEvent.SpecVersion) &&
                    attribute.Key != CloudEventAttributes.DataContentTypeAttributeName(cloudEvent.SpecVersion))
                {
                    var key = Constants.PropertyKeyPrefix + attribute.Key;
                    switch (attribute.Value)
                    {
                        case string s:
                            UserProperties.Add(key, s);
                            break;
                        case Uri uri:
                            UserProperties.Add(key, uri.ToString());
                            break;
                        case DateTime dateTime:
                            UserProperties.Add(key, dateTime.ToUniversalTime().ToString("o"));
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
