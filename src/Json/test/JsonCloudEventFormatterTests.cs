using System;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

using FluentAssertions;

using Newtonsoft.Json;

using Xunit;

namespace CloudNative.CloudEvents.Json.Tests
{
    public class JsonCloudEventFormatterTests
        : JsonCloudEventFormatterTestsBase
    {
        protected override JsonCloudEventFormatter<CustomData> Formatter { get; }
            = new JsonCloudEventFormatter<CustomData>();
    }

    public class JsonCloudEventFormatterWithSettingsTests
    : JsonCloudEventFormatterTestsBase
    {
        protected override JsonCloudEventFormatter<CustomData> Formatter { get; }
            = new JsonCloudEventFormatter<CustomData>(new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            });
    }

    public abstract class JsonCloudEventFormatterTestsBase
    {
        private readonly string _jsonPath = "event.json";

        protected abstract JsonCloudEventFormatter<CustomData> Formatter { get; }

        [Fact]
        public void ReserializeTest()
        {
            var cloudEvent = Formatter.DecodeStructuredEvent(ReadJsonFile());
            var jsonData = Formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = Formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public void ReserializeStreamTest()
        {
            CloudEvent cloudEvent;
            using (var stream = OpenJsonStream())
            {
                cloudEvent = Formatter.DecodeStructuredEvent(stream);
            }

            var jsonData = Formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = Formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public async Task ReserializeStreamAsyncTest()
        {
            CloudEvent cloudEvent;
            using (var stream = OpenJsonStream())
            {
                cloudEvent = await Formatter.DecodeStructuredEventAsync(stream);
            }

            var jsonData = Formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = Formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public void ReserializeTestV0_3toV0_1()
#pragma warning restore CA1707 // Identifiers should not contain underscores
        {
            var cloudEvent = Formatter.DecodeStructuredEvent(ReadJsonFile());
            cloudEvent.SpecVersion = CloudEventsSpecVersion.V0_1;
            var jsonData = Formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = Formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public void StructuredParseSuccess()
        {
            var cloudEvent = Formatter.DecodeStructuredEvent(ReadJsonFile());
            cloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.V1_0);
            cloudEvent.Type.Should().Be("com.github.pull.create");
            cloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            cloudEvent.Id.Should().Be("A234-1234-1234");
            cloudEvent.Time.Should().NotBeNull();
            cloudEvent.Time!.Value.ToUniversalTime().Should().Be(DateTimeOffset.Parse("2018-04-05T17:31:00Z", CultureInfo.InvariantCulture).UtcDateTime);
            cloudEvent.DataContentType.Should().Be(new ContentType(MediaTypeNames.Text.Xml));
            cloudEvent.Data.Should().Be(new CustomData { OtherValue = 6 });

            var attr = cloudEvent.GetAttributes();
            ((string)attr["comexampleextension1"]).Should().Be("value");
            ((int)((dynamic)attr["comexampleextension2"]).othervalue).Should().Be(5);
        }

        [Fact]
        public void StructuredParseWithExtensionsSuccess()
        {
            var cloudEvent = Formatter.DecodeStructuredEvent(
                ReadJsonFile(),
                new ComExampleExtension1Extension(),
                new ComExampleExtension2Extension());
            cloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.V1_0);
            cloudEvent.Type.Should().Be("com.github.pull.create");
            cloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            cloudEvent.Id.Should().Be("A234-1234-1234");
            cloudEvent.Time.Should().NotBeNull();
            cloudEvent.Time!.Value.ToUniversalTime().Should().Be(DateTimeOffset.Parse("2018-04-05T17:31:00Z", CultureInfo.InvariantCulture).UtcDateTime);
            cloudEvent.DataContentType.Should().Be(new ContentType(MediaTypeNames.Text.Xml));
            cloudEvent.Data.Should().Be(new CustomData { OtherValue = 6 });

            cloudEvent.Extension<ComExampleExtension1Extension>().ComExampleExtension1.Should().Be("value");
            cloudEvent.Extension<ComExampleExtension2Extension>().ComExampleExtension2.Should().Be(new CustomData { OtherValue = 5 });
        }

        private byte[] ReadJsonFile()
            => File.ReadAllBytes(_jsonPath);

        private Stream OpenJsonStream()
            => File.OpenRead(_jsonPath);

        private void AssertCloudEvent(CloudEvent expected, CloudEvent actual)
        {
            actual.SpecVersion.Should().Be(expected.SpecVersion);
            actual.Type.Should().Be(expected.Type);
            actual.Source.Should().Be(expected.Source);
            actual.Id.Should().Be(expected.Id);
#pragma warning disable NullConditionalAssertion // Code Smell
            (actual.Time?.ToUniversalTime()).Should().Be(expected.Time?.ToUniversalTime());
#pragma warning restore NullConditionalAssertion // Code Smell
            actual.DataContentType.Should().Be(expected.DataContentType);
            actual.Data.Should().BeOfType<CustomData>();
            expected.Data.Should().BeOfType<CustomData>();
            actual.Data.Should().Be(expected.Data);
        }
    }
}
