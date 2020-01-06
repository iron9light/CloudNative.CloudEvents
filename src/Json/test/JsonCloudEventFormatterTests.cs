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

            cloudEvent.SpecVersion.Should().Be(cloudEvent2.SpecVersion);
            cloudEvent.Type.Should().Be(cloudEvent2.Type);
            cloudEvent.Source.Should().Be(cloudEvent2.Source);
            cloudEvent.Id.Should().Be(cloudEvent2.Id);
#pragma warning disable NullConditionalAssertion // Code Smell
            (cloudEvent.Time?.ToUniversalTime()).Should().Be(cloudEvent2.Time?.ToUniversalTime());
#pragma warning restore NullConditionalAssertion // Code Smell
            cloudEvent.DataContentType.Should().Be(cloudEvent2.DataContentType);
            cloudEvent.Data.Should().BeOfType<CustomData>();
            cloudEvent2.Data.Should().BeOfType<CustomData>();
            cloudEvent.Data.Should().Be(cloudEvent2.Data);
        }

        [Fact]
        public async Task ReserializeAsyncTest()
        {
            CloudEvent cloudEvent;
            using (var stream = OpenJsonStream())
            {
                cloudEvent = await _formatter.DecodeStructuredEventAsync(stream);
            }

            var jsonData = _formatter.EncodeStructuredEvent(cloudEvent, out _);
            var cloudEvent2 = _formatter.DecodeStructuredEvent(jsonData);

            cloudEvent.SpecVersion.Should().Be(cloudEvent2.SpecVersion);
            cloudEvent.Type.Should().Be(cloudEvent2.Type);
            cloudEvent.Source.Should().Be(cloudEvent2.Source);
            cloudEvent.Id.Should().Be(cloudEvent2.Id);
#pragma warning disable NullConditionalAssertion // Code Smell
            (cloudEvent.Time?.ToUniversalTime()).Should().Be(cloudEvent2.Time?.ToUniversalTime());
#pragma warning restore NullConditionalAssertion // Code Smell
            cloudEvent.DataContentType.Should().Be(cloudEvent2.DataContentType);
            cloudEvent.Data.Should().BeOfType<CustomData>();
            cloudEvent2.Data.Should().BeOfType<CustomData>();
            cloudEvent.Data.Should().Be(cloudEvent2.Data);
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

            cloudEvent.SpecVersion.Should().Be(cloudEvent2.SpecVersion);
            cloudEvent.Type.Should().Be(cloudEvent2.Type);
            cloudEvent.Source.Should().Be(cloudEvent2.Source);
            cloudEvent.Id.Should().Be(cloudEvent2.Id);
#pragma warning disable NullConditionalAssertion // Code Smell
            (cloudEvent.Time?.ToUniversalTime()).Should().Be(cloudEvent2.Time?.ToUniversalTime());
#pragma warning restore NullConditionalAssertion // Code Smell
            cloudEvent.DataContentType.Should().Be(cloudEvent2.DataContentType);
            cloudEvent.Data.Should().BeOfType<CustomData>();
            cloudEvent2.Data.Should().BeOfType<CustomData>();
            cloudEvent.Data.Should().Be(cloudEvent2.Data);
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
    }
}
