using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using WeatherApiWrapper.Models;
using WeatherApiWrapper.Options;
using WeatherApiWrapper.Services;
using WeatherApiWrapper.Tests.TestDoubles;

namespace WeatherApiWrapper.Tests.Services
{
    public class WeatherServiceTests
    {
        private static WeatherApiOptions CreateOptions() =>
            new()
            {
                BaseUrl = "https://fake-weather-provider/",
                ApiKey = "test-key"
            };

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenCached_ReturnsFromCacheTrue()
        {
            var cached = new WeatherResponse
            {
                City = "Istanbul",
                ResolvedAddress = "Istanbul",
                TemperatureCelsius = 10.5m,
                Humidity = 50,
                WindSpeed = 12,
                Condition = "Cloudy",
                ProviderObservationTime = "12:00:00",
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false
            };

            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached)));

            var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                throw new Exception("Provider should not be called when cache hit exists.")));

            var service = new WeatherService(
                httpClient,
                cacheMock.Object,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                Mock.Of<ILogger<WeatherService>>());

            var result = await service.GetCurrentWeatherAsync("Istanbul");

            Assert.True(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal(10.5m, result.TemperatureCelsius);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenCacheMiss_CallsProviderAndCaches()
        {
            const string providerJson = """
            {
              "resolvedAddress": "Istanbul",
              "currentConditions": {
                "temp": 14.2,
                "humidity": 61.0,
                "windspeed": 18.4,
                "conditions": "Partially cloudy",
                "datetime": "18:50:00"
              }
            }
            """;

            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            cacheMock
                .Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                FakeHttpMessageHandler.JsonResponse(providerJson)));

            var service = new WeatherService(
                httpClient,
                cacheMock.Object,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                Mock.Of<ILogger<WeatherService>>());

            var result = await service.GetCurrentWeatherAsync("Istanbul");

            Assert.False(result.FromCache);
            Assert.Equal("Istanbul", result.City);
            Assert.Equal(14.2m, result.TemperatureCelsius);

            cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenProviderReturnsBadRequest_ThrowsArgumentException()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                FakeHttpMessageHandler.JsonResponse("bad request", HttpStatusCode.BadRequest)));

            var service = new WeatherService(
                httpClient,
                cacheMock.Object,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                Mock.Of<ILogger<WeatherService>>());

            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => service.GetCurrentWeatherAsync("asdasdasdxyz"));

            Assert.Equal("Invalid city value: 'asdasdasdxyz'.", ex.Message);
        }

        [Fact]
        public async Task GetCurrentWeatherAsync_WhenProviderReturnsNotFound_ThrowsKeyNotFoundException()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock
                .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                FakeHttpMessageHandler.JsonResponse("not found", HttpStatusCode.NotFound)));

            var service = new WeatherService(
                httpClient,
                cacheMock.Object,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                Mock.Of<ILogger<WeatherService>>());

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.GetCurrentWeatherAsync("UnknownCity"));

            Assert.Equal("City 'UnknownCity' not found.", ex.Message);
        }
    }
}