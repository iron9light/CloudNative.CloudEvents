namespace CloudNative.CloudEvents.AzureServiceBus
{
    internal static class Constants
    {
        public static string PropertyKeyPrefix { get; } = "cloudEvents:";

        public static string SpecVersionPropertyKey { get; } = PropertyKeyPrefix + "specversion";
    }
}
