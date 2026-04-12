using System.Text.Json.Serialization;

namespace WeatherApiWrapper.Models
{
    public class VisualCrossingResponse
    {
        [JsonPropertyName("resolvedAddress")]
        public string? ResolvedAddress { get; set; }

        [JsonPropertyName("currentConditions")]
        public CurrentConditions? CurrentConditions { get; set; }

        [JsonPropertyName("days")]
        public List<VisualCrossingDay>? Days { get; set; }
    }

    public class CurrentConditions
    {
        [JsonPropertyName("temp")]
        public decimal? Temp { get; set; }

        [JsonPropertyName("humidity")]
        public decimal? Humidity { get; set; }

        [JsonPropertyName("windspeed")]
        public decimal? WindSpeed { get; set; }

        [JsonPropertyName("conditions")]
        public string? Conditions { get; set; }

        [JsonPropertyName("datetime")]
        public string? DateTime { get; set; }
    }

    public class VisualCrossingDay
    {
        [JsonPropertyName("datetime")]
        public string? Date { get; set; }

        [JsonPropertyName("tempmax")]
        public decimal? TempMax { get; set; }

        [JsonPropertyName("tempmin")]
        public decimal? TempMin { get; set; }

        [JsonPropertyName("temp")]
        public decimal? Temp { get; set; }

        [JsonPropertyName("humidity")]
        public decimal? Humidity { get; set; }

        [JsonPropertyName("windspeed")]
        public decimal? WindSpeed { get; set; }

        [JsonPropertyName("conditions")]
        public string? Conditions { get; set; }
    }
}
