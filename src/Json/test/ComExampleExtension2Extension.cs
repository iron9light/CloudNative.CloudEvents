using System;
using System.Collections.Generic;

namespace CloudNative.CloudEvents.Json.Tests
{
    public class ComExampleExtension2Extension : ICloudEventExtension
    {
        private const string ExtensionAttribute = "comexampleextension2";

        private IDictionary<string, object> attributes = new Dictionary<string, object>();

        public ComExampleExtension2Extension()
        {
        }

        public CustomData ComExampleExtension2
        {
            get => (CustomData)attributes[ExtensionAttribute];
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
                    if (value is CustomData)
                    {
                        return true;
                    }

                    var ext = (dynamic)value;
                    value = new CustomData()
                    {
                        OtherValue = (int)ext.othervalue,
                    };
                    return true;
            }

            return false;
        }

        public Type? GetAttributeType(string name)
        {
            switch (name)
            {
                case ExtensionAttribute:
                    return typeof(CustomData);
            }

            return null;
        }
    }
}
