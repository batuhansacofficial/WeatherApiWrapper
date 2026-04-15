using System.Net;
using System.Net.Http.Json;
using WeatherApiWrapper.Models;

namespace WeatherApiWrapper.Tests
{
    public class WeatherApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public WeatherApiIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetCurrentWeather_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/weather/current?city=Istanbul");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(content));
        }

        [Fact]
        public async Task GetCurrentWeather_SecondCall_ReturnsFromCache()
        {
            var city = $"Istanbul-cache-{Guid.NewGuid():N}";

            var first = await _client.GetFromJsonAsync<WeatherResponse>(
                $"/api/weather/current?city={city}");

            var second = await _client.GetFromJsonAsync<WeatherResponse>(
                $"/api/weather/current?city={city}");

            Assert.NotNull(first);
            Assert.NotNull(second);

            Assert.False(first!.FromCache);
            Assert.True(second!.FromCache);
        }

        [Fact]
        public async Task GetForecast_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/weather/forecast?city=Istanbul&days=3");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetHistory_ReturnsOk()
        {
            var response = await _client.GetAsync("/api/weather/history?city=Istanbul&date=2026-04-01");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
