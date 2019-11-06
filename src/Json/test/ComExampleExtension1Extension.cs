using System;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.Json.Tests
{
    public class ComExampleExtension1Extension : ICloudEventExtension
    {
        private const string ExtensionAttribute = "comexampleextension1";

        private IDictionary<string, object> attributes = new Dictionary<string, object>();

        public ComExampleExtension1Extension()
        {
        }

        public string ComExampleExtension1
        {
            get => attributes[ExtensionAttribute].ToString() ?? string.Empty;
            set => attributes[ExtensionAttribute] = value;
        }

        void ICloudEventExtension.Attach(CloudEvent cloudEvent)
        {
            var eventAttributes = cloudEvent.GetAttributes();
            if (attributes == eventAttributes)
            {
                // already done
                return;
            }

            foreach (var attr in attributes)
            {
                eventAttributes[attr.Key] = attr.Value;
            }

            attributes = eventAttributes;
        }

        bool ICloudEventExtension.ValidateAndNormalize(string key, ref object value)
        {
            switch (key)
            {
                case ExtensionAttribute:
                    if (value is string)
                    {
                        return true;
                    }

                    throw new InvalidOperationException("value is missing or not a string");
            }

            return false;
        }

        public Type? GetAttributeType(string name)
        {
            return name switch
                {
                    ExtensionAttribute => typeof(string),
                    _ => null,
                };
        }
    }
}
