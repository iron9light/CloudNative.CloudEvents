using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CloudNative.CloudEvents.Json
{
    public class JsonCloudEventFormatter<T>
        : ICloudEventFormatter
    {
        private readonly JsonEventFormatter _jsonEventFormatter = new JsonEventFormatter();

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, params ICloudEventExtension[] extensions)
            => await DecodeStructuredEventAsync(data, (IEnumerable<ICloudEventExtension>)extensions);

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<ICloudEventExtension> extensions)
        {
            JObject jObject;

            using (var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true)))
            {
                jObject = await JObject.LoadAsync(jsonReader);
            }

            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(Stream data, params ICloudEventExtension[] extensions)
            => DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);

        public CloudEvent DecodeStructuredEvent(Stream data, IEnumerable<ICloudEventExtension> extensions)
        {
            JObject jObject;

            using (var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true)))
            {
                jObject = JObject.Load(jsonReader);
            }

            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions)
            => DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);

        public CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions)
        {
            var jsonText = Encoding.UTF8.GetString(data);
            var jObject = JObject.Parse(jsonText);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeJObject(JObject jObject, IEnumerable<ICloudEventExtension> extensions)
        {
            var cloudEvent = _jsonEventFormatter.DecodeJObject(jObject, extensions);
            if (cloudEvent.Data is JToken jTokenData)
            {
                T data;
                try
                {
                    data = jTokenData.ToObject<T>();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to deserialize data of type {typeof(T).FullName}", ex);
                }

                cloudEvent.Data = data;
            }

            return cloudEvent;
        }

        public byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
            => _jsonEventFormatter.EncodeStructuredEvent(cloudEvent, out contentType);

        public object? DecodeAttribute(CloudEventsSpecVersion specVersion, string name, byte[] data, IEnumerable<ICloudEventExtension> extensions)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Equals(CloudEventAttributes.DataAttributeName(specVersion), StringComparison.Ordinal))
            {
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data));
            }

            return _jsonEventFormatter.DecodeAttribute(specVersion, name, data, extensions);
        }

        public byte[] EncodeAttribute(CloudEventsSpecVersion specVersion, string name, object value, IEnumerable<ICloudEventExtension> extensions)
            => _jsonEventFormatter.EncodeAttribute(specVersion, name, value, extensions);
    }
}
