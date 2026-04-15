using Microsoft.AspNetCore.Mvc;
using WeatherApiWrapper.Models;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;

        public WeatherController(IWeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(
            [FromQuery] string? city,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest(CreateErrorResponse(400, "Query parameter 'city' is required."));

            var result = await _weatherService.GetCurrentWeatherAsync(city, cancellationToken);
            return Ok(result);
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast(
            [FromQuery] string? city,
            [FromQuery] int days = 3,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest(CreateErrorResponse(400, "Query parameter 'city' is required."));

            if (days < 1 || days > 10)
                return BadRequest(CreateErrorResponse(400, "Query parameter 'days' must be between 1 and 10."));

            var result = await _weatherService.GetForecastAsync(city, days, cancellationToken);
            return Ok(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string? city,
            [FromQuery] DateOnly date,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest(CreateErrorResponse(400, "Query parameter 'city' is required."));

            if (date == default)
                return BadRequest(CreateErrorResponse(400, "Query parameter 'date' is required and must be in yyyy-MM-dd format."));

            if (date > DateOnly.FromDateTime(DateTime.UtcNow.Date))
                return BadRequest(CreateErrorResponse(400, "Date cannot be in the future."));

            var result = await _weatherService.GetHistoricalWeatherAsync(city, date, cancellationToken);
            return Ok(result);
        }

        private ApiErrorResponse CreateErrorResponse(int statusCode, string message)
        {
            return new ApiErrorResponse
            {
                StatusCode = statusCode,
                Message = message,
                TraceId = HttpContext.TraceIdentifier,
                TimestampUtc = DateTime.UtcNow,
                Errors = null
            };
        }
    }
}
