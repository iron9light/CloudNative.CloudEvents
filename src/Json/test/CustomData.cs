using System;

namespace CloudNative.CloudEvents.Json.Tests
{
    public sealed class CustomData
        : IEquatable<CustomData>
    {
        public int OtherValue { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is CustomData other)
            {
                return Equals(other);
            }

            return false;
        }

        public bool Equals(CustomData? other)
        {
            if (other == null)
            {
                return false;
            }

            return OtherValue == other.OtherValue;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OtherValue);
        }
    }
}
