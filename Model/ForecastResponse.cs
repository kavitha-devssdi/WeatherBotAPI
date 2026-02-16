namespace WeatherBotAPI.Model
{
    public class ForecastResponse
    {
        public string City { get; set; }
        public List<WeatherResponse> Forecasts { get; set; }
    }
}
