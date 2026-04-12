using Microsoft.AspNetCore.Mvc;
using WeatherApiWrapper.Services;

namespace WeatherApiWrapper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(
            IWeatherService weatherService,
            ILogger<WeatherController> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(
            [FromQuery] string city,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest(new
                {
                    message = "Query parameter 'city' is required."
                });
            }

            try
            {
                var result = await _weatherService.GetCurrentWeatherAsync(city, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Configuration or parsing error.");
                return StatusCode(500, new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "External provider request failed.");
                return StatusCode(502, new
                {
                    message = "Failed to fetch weather data from external provider."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error.");
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred."
                });
            }
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast(
            [FromQuery] string city,
            [FromQuery] int days = 3,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest(new
                {
                    message = "Query parameter 'city' is required."
                });
            }

            if (days < 1 || days > 10)
            {
                return BadRequest(new
                {
                    message = "Query parameter 'days' must be between 1 and 10."
                });
            }

            try
            {
                var result = await _weatherService.GetForecastAsync(city, days, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Configuration or parsing error.");
                return StatusCode(500, new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "External provider request failed.");
                return StatusCode(502, new
                {
                    message = "Failed to fetch weather data from external provider."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error.");
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred."
                });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string city,
            [FromQuery] DateOnly date,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest(new
                {
                    message = "Query parameter 'city' is required."
                });
            }

            if (date == default)
            {
                return BadRequest(new
                {
                    message = "Query parameter 'date' is required and must be in yyyy-MM-dd format."
                });
            }

            try
            {
                var result = await _weatherService.GetHistoricalWeatherAsync(city, date, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Configuration or parsing error.");
                return StatusCode(500, new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "External provider request failed.");
                return StatusCode(502, new
                {
                    message = "Failed to fetch weather data from external provider."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error.");
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred."
                });
            }
        }
    }
}
