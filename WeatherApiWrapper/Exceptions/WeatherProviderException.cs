namespace WeatherApiWrapper.Exceptions
{
    public sealed class WeatherProviderException : Exception
    {
        public WeatherProviderException(string message)
            : base(message)
        {
        }

        public WeatherProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
