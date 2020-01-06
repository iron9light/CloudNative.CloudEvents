# Extensions for CloudNative.CloudEvents

[![Build Status](https://iron9light.visualstudio.com/github/_apis/build/status/iron9light.CloudNative.CloudEvents?branchName=master)](https://iron9light.visualstudio.com/github/_build/latest?definitionId=3&branchName=master)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=iron9light_CloudNative.CloudEvents&metric=ncloc)](https://sonarcloud.io/dashboard?id=iron9light_CloudNative.CloudEvents)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=iron9light_CloudNative.CloudEvents&metric=coverage)](https://sonarcloud.io/dashboard?id=iron9light_CloudNative.CloudEvents)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=iron9light_CloudNative.CloudEvents&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=iron9light_CloudNative.CloudEvents)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=iron9light_CloudNative.CloudEvents&metric=security_rating)](https://sonarcloud.io/dashboard?id=iron9light_CloudNative.CloudEvents)

## CloudNative.CloudEvents.AzureServiceBus

[Azure ServiceBus](https://github.com/Azure/azure-sdk-for-net/tree/master/sdk/servicebus/Microsoft.Azure.ServiceBus) extension for [CloudNative.CloudEvents](https://github.com/cloudevents/spec)

[![NuGet](https://img.shields.io/nuget/v/CloudNative.CloudEvents.AzureServiceBus.svg)](https://www.nuget.org/packages/CloudNative.CloudEvents.AzureServiceBus/)

## CloudNative.CloudEvents.Json

Generic Json extension for [CloudNative.CloudEvents](https://github.com/cloudevents/spec)

[![NuGet](https://img.shields.io/nuget/v/CloudNative.CloudEvents.Json.svg)](https://www.nuget.org/packages/CloudNative.CloudEvents.Json/)

```csharp
var formatter = new JsonCloudEventFormatter<MyData>();
var cloudEvent = formatter.DecodeStructuredEvent(jsonData);
cloudEvent.Data.Should().BeOfType<MyData>(); // The type of Data is MyData type, but not JToken
```
