using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WeatherApiWrapper.Controllers;
using WeatherApiWrapper.Models;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Tests.Controllers
{
    public class WeatherControllerTests
    {
        private readonly Mock<IWeatherService> _weatherServiceMock;
        private readonly WeatherController _controller;

        public WeatherControllerTests()
        {
            _weatherServiceMock = new Mock<IWeatherService>();

            _controller = new WeatherController(_weatherServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        [Fact]
        public async Task GetCurrent_WhenCityIsMissing_ReturnsBadRequest()
        {
            var result = await _controller.GetCurrent(null, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);

            Assert.Equal(400, response.StatusCode);
            Assert.Equal("Query parameter 'city' is required.", response.Message);
        }

        [Fact]
        public async Task GetForecast_WhenDaysIsInvalid_ReturnsBadRequest()
        {
            var result = await _controller.GetForecast("Istanbul", 0, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);

            Assert.Equal(400, response.StatusCode);
            Assert.Equal("Query parameter 'days' must be between 1 and 10.", response.Message);
        }

        [Fact]
        public async Task GetHistory_WhenDateIsFuture_ReturnsBadRequest()
        {
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

            var result = await _controller.GetHistory("Istanbul", futureDate, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);

            Assert.Equal(400, response.StatusCode);
            Assert.Equal("Date cannot be in the future.", response.Message);
        }

        [Fact]
        public async Task GetCurrent_WhenRequestIsValid_ReturnsOk()
        {
            var expected = new WeatherResponse
            {
                City = "Istanbul",
                ResolvedAddress = "Istanbul",
                TemperatureCelsius = 12.3m,
                Condition = "Cloudy",
                RetrievedAtUtc = DateTime.UtcNow,
                FromCache = false
            };

            _weatherServiceMock
                .Setup(x => x.GetCurrentWeatherAsync("Istanbul", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await _controller.GetCurrent("Istanbul", CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<WeatherResponse>(ok.Value);

            Assert.Equal("Istanbul", response.City);
            Assert.Equal(12.3m, response.TemperatureCelsius);
        }
    }
}