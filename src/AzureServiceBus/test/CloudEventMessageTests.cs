using System;
using System.Globalization;
using System.Linq;
using System.Net.Mime;

using CloudNative.CloudEvents.NewtonsoftJson;

using FluentAssertions;

using Microsoft.Azure.ServiceBus;

using Xunit;

namespace CloudNative.CloudEvents.AzureServiceBus.Tests
{
    public class CloudEventMessageTests
    {
        private readonly CloudEventFormatter _formatter = new JsonEventFormatter();

        [Fact]
#pragma warning disable S2699 // Tests should include assertions
        public void ServiceBusStructuredMessageTest()
#pragma warning restore S2699 // Tests should include assertions
        {
            ServiceBusMessageTest(cloudEvent => cloudEvent.ToServiceBusMessage(ContentMode.Structured, _formatter));
        }

        [Fact]
#pragma warning disable S2699 // Tests should include assertions
        public void ServiceBusBinaryMessageTest()
#pragma warning restore S2699 // Tests should include assertions
        {
            ServiceBusMessageTest(cloudEvent => cloudEvent.ToServiceBusMessage(ContentMode.Binary, _formatter));
        }

        private void ServiceBusMessageTest(Func<CloudEvent, Message> event2message)
        {
            var data = "<much wow=\"xml\"/>";
            var cloudEvent = new CloudEvent
            {
                Type = "com.github.pull.create",
                Source = new Uri("https://github.com/cloudevents/spec/pull"),
                Subject = "123",
                Id = "A234-1234-1234",
                Time = new DateTimeOffset(2018, 4, 5, 17, 31, 0, TimeSpan.Zero),
                DataContentType = MediaTypeNames.Text.Xml,
                Data = data,
                ["comexampleextension1"] = "value",
            };

            var message = event2message(cloudEvent);
            message.IsCloudEvent().Should().BeTrue();

            var clonedMessage = message.Clone();
            clonedMessage.IsCloudEvent().Should().BeTrue();

            var receivedCloudEvent = clonedMessage.ToCloudEvent(_formatter);

            receivedCloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.Default);
            receivedCloudEvent.Type.Should().Be("com.github.pull.create");
            receivedCloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            receivedCloudEvent.Subject.Should().Be("123");
            receivedCloudEvent.Id.Should().Be("A234-1234-1234");
            receivedCloudEvent.Time.Should().NotBeNull();
            receivedCloudEvent.Time!.Value.ToUniversalTime().Should().Be(DateTime.Parse("2018-04-05T17:31:00Z", CultureInfo.InvariantCulture).ToUniversalTime());
            receivedCloudEvent.DataContentType.Should().Be(MediaTypeNames.Text.Xml);
            receivedCloudEvent.Data.Should().Be(data);

            var receivedAttrs = receivedCloudEvent.GetPopulatedAttributes();
            var attrPair1 = receivedAttrs.FirstOrDefault(x => x.Key.Name == "comexampleextension1");
            attrPair1.Should().NotBeNull();
            attrPair1.Key.Type.Should().Be(CloudEventAttributeType.String);
            ((string)attrPair1.Value).Should().Be("value");
        }
    }
}
