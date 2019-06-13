using System.IO;
using System.Text;

using Microsoft.Azure.ServiceBus;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    public class CloudEventMessage : Message
    {
        public CloudEventMessage(CloudEvent cloudEvent, ContentMode contentMode, ICloudEventFormatter formatter)
        {
            MessageId = cloudEvent.Id;

            if (contentMode == ContentMode.Structured)
            {
                Body = formatter.EncodeStructuredEvent(cloudEvent, out var contentType);
                ContentType = contentType.MediaType;
            }
            else
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
                    case string s:
                        Body = GetEncoding(cloudEvent).GetBytes(s);
                        break;
                }

                ContentType = cloudEvent.DataContentType?.MediaType;
            }

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
                    UserProperties.Add(Constants.PropertyKeyPrefix + attribute.Key, attribute.Value);
                }
            }
        }

        private static Encoding GetEncoding(CloudEvent cloudEvent)
        {
            if (cloudEvent.DataContentEncoding != null)
            {
                return Encoding.GetEncoding(cloudEvent.DataContentEncoding);
            }
            else
            {
                return Constants.DefaultEncoding;
            }
        }
    }
}
