# Weather API Wrapper

A simple ASP.NET Core Web API project that wraps an external weather provider and exposes normalized endpoints for current, forecast, and historical weather data.
<br>
Project Idea: [Weather API](https://roadmap.sh/projects/weather-api-wrapper-service)

## Features

- Current weather endpoint
- Forecast weather endpoint
- Historical weather endpoint
- In-memory caching
- Swagger / OpenAPI support
- External provider integration
- API key via User Secrets
- Basic validation and error handling

## Tech Stack

- ASP.NET Core Web API
- .NET
- Visual Studio
- IMemoryCache
- HttpClient
- Visual Crossing Weather API

## Project Structure

```text
WeatherApiWrapper/
 ├─ Controllers/
 │   └─ WeatherController.cs
 ├─ Models/
 │   ├─ ForecastDayResponse.cs
 │   ├─ ForecastResponse.cs
 │   ├─ HistoricalDayResponse.cs
 │   ├─ HistoryResponse.cs
 │   ├─ VisualCrossingResponse.cs
 │   └─ WeatherResponse.cs
 ├─ Options/
 │   └─ WeatherApiOptions.cs
 ├─ Services/
 │   ├─ IWeatherService.cs
 │   └─ WeatherService.cs
 ├─ Program.cs
 ├─ appsettings.json
 └─ WeatherApiWrapper.csproj
```

## Endpoints

**Note**: The example responses below are sample outputs captured during local testing.

### 1. Current Weather

```http
GET /api/weather/current?city=Istanbul
```

Example response:

```json
{
  "city": "Istanbul",
  "resolvedAddress": "Istanbul",
  "temperatureCelsius": 13.8,
  "humidity": 38.9,
  "windSpeed": 12.6,
  "condition": "Partially cloudy",
  "providerObservationTime": "16:50:00",
  "retrievedAtUtc": "2026-04-12T14:20:11.6427296Z",
  "fromCache": false
}
```

---

### 2. Forecast Weather

```http
GET /api/weather/forecast?city=Istanbul&days=3
```

Example response:

```json
{
  "city": "Istanbul",
  "resolvedAddress": "Istanbul",
  "daysRequested": 3,
  "retrievedAtUtc": "2026-04-12T14:31:22.7307341Z",
  "fromCache": false,
  "forecasts": [
    {
      "date": "2026-04-12",
      "temperatureMaxCelsius": 13.8,
      "temperatureMinCelsius": 6.4,
      "humidity": 56.3,
      "windSpeed": 16.2,
      "condition": "Partially cloudy"
    },
    {
      "date": "2026-04-13",
      "temperatureMaxCelsius": 13.3,
      "temperatureMinCelsius": 8.3,
      "humidity": 64.3,
      "windSpeed": 17.6,
      "condition": "Partially cloudy"
    }
  ]
}
```

---

### 3. Historical Weather

```http
GET /api/weather/history?city=Istanbul&date=2026-04-01
```

Example response:

```json
{
  "city": "Istanbul",
  "resolvedAddress": "Istanbul",
  "requestedDate": "2026-04-01",
  "retrievedAtUtc": "2026-04-12T20:44:17.8124659Z",
  "fromCache": false,
  "history": {
    "date": "2026-04-01",
    "temperatureMaxCelsius": 19.1,
    "temperatureMinCelsius": 9.8,
    "temperatureAverageCelsius": 13.6,
    "humidity": 71.3,
    "windSpeed": 37.3,
    "condition": "Rain, Partially cloudy"
  }
}
```

## Validation Rules

### Current

* `city` is required

### Forecast

* `city` is required
* `days` must be between `1` and `10`

### History

* `city` is required
* `date` is required
* `date` must be in `yyyy-MM-dd` format
* `date` cannot be in the future

## Setup

### 1. Clone the project

```bash
git clone <your-repository-url>
cd WeatherApiWrapper
```

### 2. Initialize user secrets

```bash
dotnet user-secrets init
```

### 3. Set the API key

```bash
dotnet user-secrets set "WeatherApi:ApiKey" "YOUR_API_KEY"
```

### 4. Run the project

```bash
dotnet run
```

### 5. Open Swagger

```text
https://localhost:7081/swagger
```

or

```text
http://localhost:5158/swagger
```

## Configuration

`appsettings.json`

```json
{
  "WeatherApi": {
    "BaseUrl": "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/"
  }
}
```

API key is intentionally not stored in source code. Use User Secrets or environment variables.

## Caching

This project uses `IMemoryCache`.

Cache strategy:

* Current weather: short expiration
* Forecast: medium expiration
* History: longer expiration

## Error Handling

Typical responses:

* `200 OK` -> success
* `400 Bad Request` -> invalid input
* `404 Not Found` -> location not found
* `500 Internal Server Error` -> parsing/configuration error
* `502 Bad Gateway` -> external provider failure

## Future Improvements

* Redis distributed cache
* Global exception middleware
* Unified error response model
* Logging improvements
* Unit tests
* Docker support
* API versioning

## License

This project is licensed under the MIT License. See the [LICENSE](https://github.com/batuhansacofficial/WeatherApiWrapper?tab=MIT-1-ov-file) file for details.

This project was built for learning and portfolio purposes.
