using System;
using System.Net.Mime;
using System.Text;

using FluentAssertions;

using Xunit;

namespace CloudNative.CloudEvents.AzureServiceBus.Tests
{
    public class CloudEventMessageTests
    {
        [Fact]
#pragma warning disable S2699 // Tests should include assertions
        public void ServiceBusStructuredMessageTest()
#pragma warning restore S2699 // Tests should include assertions
        {
            ServiceBusMessageTest(cloudEvent => new ServiceBusCloudEventMessage(cloudEvent, new JsonEventFormatter()), ContentMode.Structured);
        }

        [Fact]
#pragma warning disable S2699 // Tests should include assertions
        public void ServiceBusBinaryMessageTest()
#pragma warning restore S2699 // Tests should include assertions
        {
            ServiceBusMessageTest(cloudEvent => new ServiceBusCloudEventMessage(cloudEvent), ContentMode.Binary);
        }

        private void ServiceBusMessageTest(Func<CloudEvent, ServiceBusCloudEventMessage> event2message, ContentMode contentMode)
        {
            var data = "<much wow=\"xml\"/>";
            var cloudEvent = new CloudEvent(
                CloudEventsSpecVersion.V1_0,
                "com.github.pull.create",
                source: new Uri("https://github.com/cloudevents/spec/pull"),
                subject: "123")
            {
                Id = "A234-1234-1234",
                Time = new DateTime(2018, 4, 5, 17, 31, 0, DateTimeKind.Utc),
                DataContentType = new ContentType(MediaTypeNames.Text.Xml),
                Data = contentMode == ContentMode.Structured ? (object)data : (object)Encoding.UTF8.GetBytes(data),
            };

            var attrs = cloudEvent.GetAttributes();
            attrs["comexampleextension1"] = "value";
            attrs["comexampleextension2"] = new { othervalue = 5 };

            var message = event2message(cloudEvent);
            message.IsCloudEvent().Should().BeTrue();

            var clonedMessage = message.Clone();
            clonedMessage.IsCloudEvent().Should().BeTrue();

            var receivedCloudEvent = clonedMessage.ToCloudEvent();

            receivedCloudEvent.SpecVersion.Should().Be(CloudEventsSpecVersion.Default);
            receivedCloudEvent.Type.Should().Be("com.github.pull.create");
            receivedCloudEvent.Source.Should().Be(new Uri("https://github.com/cloudevents/spec/pull"));
            receivedCloudEvent.Subject.Should().Be("123");
            receivedCloudEvent.Id.Should().Be("A234-1234-1234");
            receivedCloudEvent.Time.Should().NotBeNull();
            receivedCloudEvent.Time?.ToUniversalTime().Should().Be(DateTime.Parse("2018-04-05T17:31:00Z").ToUniversalTime());
            receivedCloudEvent.DataContentType.Should().Be(new ContentType(MediaTypeNames.Text.Xml));

            if (contentMode == ContentMode.Structured)
            {
                receivedCloudEvent.Data.Should().Be(data);
            }
            else
            {
                Encoding.UTF8.GetString((byte[])receivedCloudEvent.Data).Should().Be(data);
            }

            var receivedAttrs = receivedCloudEvent.GetAttributes();
            ((string)receivedAttrs["comexampleextension1"]).Should().Be("value");
            ((int)((dynamic)receivedAttrs["comexampleextension2"]).othervalue).Should().Be(5);
        }
    }
}
