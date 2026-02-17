namespace WeatherBotAPI.Model
{
    public class CurrentWeatherResponse
    {
        public string City { get; set; }
        public WeatherResponse Weather { get; set; }
    }
}
