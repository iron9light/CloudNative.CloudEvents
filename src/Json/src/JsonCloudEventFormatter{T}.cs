using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private readonly JsonSerializerSettings? _settings;
        private readonly JsonSerializer _serializer;

        public JsonCloudEventFormatter(JsonSerializerSettings? settings = null)
        {
            _settings = settings;
            _serializer = JsonSerializer.CreateDefault(settings);
        }

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, params ICloudEventExtension[] extensions)
            => await DecodeStructuredEventAsync(data, (IEnumerable<ICloudEventExtension>)extensions);

        public async Task<CloudEvent> DecodeStructuredEventAsync(Stream data, IEnumerable<ICloudEventExtension> extensions)
        {
            JObject jObject;

            using (var jsonReader = new JsonTextReader(new StreamReader(data, Encoding.UTF8, true, 8192, true)))
            {
                if (_settings == null)
                {
                    jObject = await JObject.LoadAsync(jsonReader);
                }
                else
                {
                    jObject = _serializer.Deserialize<JObject>(jsonReader);
                }
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
                if (_settings == null)
                {
                    jObject = JObject.Load(jsonReader);
                }
                else
                {
                    jObject = _serializer.Deserialize<JObject>(jsonReader);
                }
            }

            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeStructuredEvent(byte[] data, params ICloudEventExtension[] extensions)
            => DecodeStructuredEvent(data, (IEnumerable<ICloudEventExtension>)extensions);

        public CloudEvent DecodeStructuredEvent(byte[] data, IEnumerable<ICloudEventExtension> extensions)
        {
            var jsonText = Encoding.UTF8.GetString(data);
            var jObject = JsonConvert.DeserializeObject<JObject>(jsonText, _settings);
            return DecodeJObject(jObject, extensions);
        }

        public CloudEvent DecodeJObject(JObject jObject, IEnumerable<ICloudEventExtension> extensions)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            var specVersion = GetSpecVersion(jObject);
            var cloudEvent = new CloudEvent(specVersion, extensions);
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in jObject)
            {
                // skip the version since we set that above
                if (keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1), StringComparison.OrdinalIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_2), StringComparison.OrdinalIgnoreCase) ||
                    keyValuePair.Key.Equals(CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V1_0), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (
                    specVersion == CloudEventsSpecVersion.V1_0 &&
                    keyValuePair.Key.Equals("data_base64", StringComparison.OrdinalIgnoreCase)
                    )
                {
                    attributes["data"] = Convert.FromBase64String(keyValuePair.Value.ToString());
                    continue;
                }

                if (keyValuePair.Key.Equals(CloudEventAttributes.DataAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<T>(_serializer);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to deserialize data of type {typeof(T).FullName}", ex);
                    }
                }

                switch (keyValuePair.Value.Type)
                {
                    case JTokenType.String:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<string>(_serializer);
                        break;
                    case JTokenType.Date:
                        var timeValue = ((JValue)keyValuePair.Value).Value; // The value type may be DateTime or DateTimeOffset
                        if (keyValuePair.Key.Equals(CloudEventAttributes.TimeAttributeName(specVersion), StringComparison.OrdinalIgnoreCase) &&
                            timeValue is DateTimeOffset dateTimeOffset)
                        {
                            attributes[keyValuePair.Key] = dateTimeOffset.UtcDateTime;
                        }
                        else
                        {
                            attributes[keyValuePair.Key] = timeValue;
                        }

                        break;
                    case JTokenType.Uri:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<Uri>(_serializer);
                        break;
                    case JTokenType.Null:
                        attributes[keyValuePair.Key] = null;
                        break;
                    case JTokenType.Integer:
                        attributes[keyValuePair.Key] = keyValuePair.Value.ToObject<int>(_serializer);
                        break;
                    default:
                        attributes[keyValuePair.Key] = (dynamic)keyValuePair.Value;
                        break;
                }
            }

            return cloudEvent;
        }

        public byte[] EncodeStructuredEvent(CloudEvent cloudEvent, out ContentType contentType)
        {
            if (cloudEvent == null)
            {
                throw new ArgumentNullException(nameof(cloudEvent));
            }

            contentType = new ContentType("application/cloudevents+json")
            {
                CharSet = Encoding.UTF8.WebName,
            };

            var jObject = new JObject();
            var attributes = cloudEvent.GetAttributes();
            foreach (var keyValuePair in attributes)
            {
                if (keyValuePair.Value == null)
                {
                    continue;
                }

                if (keyValuePair.Value is ContentType contentTypeValue &&
                    !string.IsNullOrEmpty(contentTypeValue.MediaType))
                {
                    jObject[keyValuePair.Key] = contentTypeValue.ToString();
                }
                else if (cloudEvent.SpecVersion == CloudEventsSpecVersion.V1_0 &&
                         keyValuePair.Key.Equals(CloudEventAttributes.DataAttributeName(cloudEvent.SpecVersion), StringComparison.OrdinalIgnoreCase))
                {
                    if (keyValuePair.Value is Stream stream)
                    {
                        using (var sr = new BinaryReader(stream))
                        {
                            jObject["data_base64"] = Convert.ToBase64String(sr.ReadBytes((int)sr.BaseStream.Length));
                        }
                    }
                    else if (keyValuePair.Value is IEnumerable<byte> bytes)
                    {
                        jObject["data_base64"] = Convert.ToBase64String(bytes.ToArray());
                    }
                    else
                    {
                        jObject["data"] = JToken.FromObject(keyValuePair.Value, _serializer);
                    }
                }
                else
                {
                    jObject[keyValuePair.Key] = JToken.FromObject(keyValuePair.Value, _serializer);
                }
            }

            return Encoding.UTF8.GetBytes(jObject.ToString());
        }

        public object? DecodeAttribute(CloudEventsSpecVersion specVersion, string name, byte[] data, IEnumerable<ICloudEventExtension> extensions)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var json = Encoding.UTF8.GetString(data);

            if (name.Equals(CloudEventAttributes.DataAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
            {
                return JsonConvert.DeserializeObject<T>(json, _settings);
            }

            if (name.Equals(CloudEventAttributes.IdAttributeName(specVersion), StringComparison.OrdinalIgnoreCase) ||
                name.Equals(CloudEventAttributes.TypeAttributeName(specVersion), StringComparison.OrdinalIgnoreCase) ||
                name.Equals(CloudEventAttributes.SubjectAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
            {
                return JsonConvert.DeserializeObject<string>(json, _settings);
            }

            if (name.Equals(CloudEventAttributes.TimeAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
            {
                return JsonConvert.DeserializeObject<DateTime>(json, _settings);
            }

            if (name.Equals(CloudEventAttributes.SourceAttributeName(specVersion), StringComparison.OrdinalIgnoreCase) ||
                name.Equals(CloudEventAttributes.DataSchemaAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
            {
                return JsonConvert.DeserializeObject<Uri>(json, _settings);
            }

            if (name.Equals(CloudEventAttributes.DataContentTypeAttributeName(specVersion), StringComparison.OrdinalIgnoreCase))
            {
                var contentType = JsonConvert.DeserializeObject<string>(json, _settings);
                return new ContentType(contentType);
            }

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    Type type = extension.GetAttributeType(name);
                    if (type != null)
                    {
                        return JsonConvert.DeserializeObject(json, type, _settings);
                    }
                }
            }

            return JsonConvert.DeserializeObject(json, _settings);
        }

        public byte[] EncodeAttribute(CloudEventsSpecVersion specVersion, string name, object value, IEnumerable<ICloudEventExtension> extensions)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    var written = false;
                    if (name.Equals(CloudEventAttributes.DataAttributeName(specVersion), StringComparison.OrdinalIgnoreCase) &&
                        value is Stream streamValue)
                    {
                        using (var buffer = new MemoryStream())
                        {
                            streamValue.CopyTo(buffer);
                            _serializer.Serialize(writer, buffer.ToArray());
                            written = true;
                        }
                    }

                    if (!written && extensions != null)
                    {
                        foreach (var extension in extensions)
                        {
                            var type = extension.GetAttributeType(name);
                            if (type != null)
                            {
                                _serializer.Serialize(writer, Convert.ChangeType(value, type, CultureInfo.InvariantCulture));
                                written = true;
                                break;
                            }
                        }
                    }

                    if (!written)
                    {
                        _serializer.Serialize(writer, value);
                    }
                }

                return stream.ToArray();
            }
        }

        private static CloudEventsSpecVersion GetSpecVersion(JObject jObject)
        {
            var specVersion = CloudEventsSpecVersion.Default;
            if (jObject.GetValue(
                CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1),
                StringComparison.OrdinalIgnoreCase
                ) != null)
            {
                specVersion = CloudEventsSpecVersion.V0_1;
            }
            else if (jObject.TryGetValue(
                CloudEventAttributes.SpecVersionAttributeName(CloudEventsSpecVersion.V0_1),
                StringComparison.OrdinalIgnoreCase,
                out var jToken
                ))
            {
                var version = (string)jToken;
                if (version == "0.2")
                {
                    specVersion = CloudEventsSpecVersion.V0_2;
                }
                else if (version == "0.3")
                {
                    specVersion = CloudEventsSpecVersion.V0_3;
                }
            }

            return specVersion;
        }
    }
}
