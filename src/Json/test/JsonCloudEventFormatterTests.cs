using System;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

namespace CloudNative.CloudEvents.Json.Tests
{
    public class JsonCloudEventFormatterTests
    {
        private readonly string _jsonPath = "event.json";

        private readonly JsonCloudEventFormatter<CustomData> _formatter = new JsonCloudEventFormatter<CustomData>();

        [Fact]
        public void ReserializeTest()
        {
            var cloudEvent = _formatter.DecodeStructuredEvent(ReadJsonFile());
            var jsonData = _formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = _formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public void ReserializeStreamTest()
        {
            CloudEvent cloudEvent;
            using (var stream = OpenJsonStream())
            {
                cloudEvent = _formatter.DecodeStructuredEvent(stream);
            }

            var jsonData = _formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = _formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public async Task ReserializeStreamAsyncTest()
        {
            CloudEvent cloudEvent;
            using (var stream = OpenJsonStream())
            {
                cloudEvent = await _formatter.DecodeStructuredEventAsync(stream);
            }

            var jsonData = _formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = _formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public void ReserializeTestV0_3toV0_1()
#pragma warning restore CA1707 // Identifiers should not contain underscores
        {
            var cloudEvent = _formatter.DecodeStructuredEvent(ReadJsonFile());
            cloudEvent.SpecVersion = CloudEventsSpecVersion.V0_1;
            var jsonData = _formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = _formatter.DecodeStructuredEvent(jsonData);

            AssertCloudEvent(cloudEvent, cloudEvent2);
        }

        [Fact]
        public void StructuredParseSuccess()
        {
            var cloudEvent = _formatter.DecodeStructuredEvent(ReadJsonFile());
            cloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.V1_0);
            cloudEvent.Type.Should().Be("com.github.pull.create");
            cloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            cloudEvent.Id.Should().Be("A234-1234-1234");
            cloudEvent.Time.Should().NotBeNull();
            cloudEvent.Time!.Value.ToUniversalTime().Should().Be(DateTime.Parse("2018-04-05T17:31:00Z", CultureInfo.InvariantCulture).ToUniversalTime());
            cloudEvent.DataContentType.Should().Be(new ContentType(MediaTypeNames.Text.Xml));
            cloudEvent.Data.Should().Be(new CustomData { OtherValue = 6 });

            var attr = cloudEvent.GetAttributes();
            ((string)attr["comexampleextension1"]).Should().Be("value");
            ((int)((dynamic)attr["comexampleextension2"]).othervalue).Should().Be(5);
        }

        [Fact]
        public void StructuredParseWithExtensionsSuccess()
        {
            var cloudEvent = _formatter.DecodeStructuredEvent(
                ReadJsonFile(),
                new ComExampleExtension1Extension(),
                new ComExampleExtension2Extension());
            cloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.V1_0);
            cloudEvent.Type.Should().Be("com.github.pull.create");
            cloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            cloudEvent.Id.Should().Be("A234-1234-1234");
            cloudEvent.Time.Should().NotBeNull();
            cloudEvent.Time!.Value.ToUniversalTime().Should().Be(DateTime.Parse("2018-04-05T17:31:00Z", CultureInfo.InvariantCulture).ToUniversalTime());
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
