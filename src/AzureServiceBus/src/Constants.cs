using System.Text;

namespace CloudNative.CloudEvents.AzureServiceBus
{
    internal static class Constants
    {
        public static string PropertyKeyPrefix { get; } = "cloudEvents:";

        public static string SpecVersion1PropertyKey { get; } = PropertyKeyPrefix + "cloudEventsVersion";

        public static string SpecVersion2PropertyKey { get; } = PropertyKeyPrefix + "specversion";
    }
}
