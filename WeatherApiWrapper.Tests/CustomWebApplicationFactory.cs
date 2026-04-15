using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherApiWrapper.Services;
using WeatherApiWrapper.Tests.TestDoubles;

namespace WeatherApiWrapper.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWeatherService>();
                services.RemoveAll<IDistributedCache>();

                services.AddDistributedMemoryCache();

                services.AddHttpClient<IWeatherService, WeatherService>()
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new FakeHttpMessageHandler(_ =>
                            FakeHttpMessageHandler.JsonResponse("""
                        {
                          "resolvedAddress": "Istanbul",
                          "currentConditions": {
                            "temp": 20,
                            "humidity": 50,
                            "windspeed": 10,
                            "conditions": "Clear",
                            "datetime": "12:00:00"
                          },
                          "days": [
                            {
                              "datetime": "2026-04-15",
                              "tempmax": 21,
                              "tempmin": 12,
                              "temp": 17,
                              "humidity": 55,
                              "windspeed": 11,
                              "conditions": "Clear"
                            },
                            {
                              "datetime": "2026-04-16",
                              "tempmax": 22,
                              "tempmin": 13,
                              "temp": 18,
                              "humidity": 54,
                              "windspeed": 12,
                              "conditions": "Partially cloudy"
                            },
                            {
                              "datetime": "2026-04-17",
                              "tempmax": 23,
                              "tempmin": 14,
                              "temp": 19,
                              "humidity": 53,
                              "windspeed": 13,
                              "conditions": "Cloudy"
                            }
                          ]
                        }
                        """)));
            });
        }
    }
}