using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WeatherBotAPI.Helpers;
using WeatherBotAPI.Model;

namespace WeatherBotAPI.Controllers
{
    [ApiController]
    [Route("api/weather")]
    public class WeatherController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public WeatherController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        // ================= CURRENT WEATHER =================

        [HttpGet("current")]
        [ProducesResponseType(typeof(CurrentWeatherResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CurrentWeatherResponse>> GetCurrentWeather([FromQuery] string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest("City is required.");

            var weatherData = await GetWeatherFromAPI(city);
            if (weatherData == null)
                return NotFound("Unable to fetch weather data.");

            return Ok(new CurrentWeatherResponse
            {
                City = city,
                Weather = new WeatherResponse
                {
                    DateTimeIst = TimeZoneHelper.ConvertUtcToIst(DateTime.UtcNow),
                    Temperature = weatherData.Temperature,
                    Humidity = weatherData.Humidity,
                    Condition = weatherData.Condition
                }
            });
        }

        // ================= GENERAL FORECAST =================

        [HttpGet("forecast")]
        [ProducesResponseType(typeof(ForecastResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ForecastResponse>> GetForecast([FromQuery] string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest("City is required.");

            var forecast = await GetForecastFromAPI(city);
            if (forecast == null)
                return NotFound("Unable to fetch forecast data.");

            return Ok(forecast);
        }

        // ================= FORECAST BY DATE =================

        [HttpGet("forecastByDate")]
        [ProducesResponseType(typeof(CurrentWeatherResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CurrentWeatherResponse>> GetForecastByDate(
            [FromQuery] string city,
            [FromQuery] DateTime dateTime)
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest("City is required.");

            var requestedUtc = TimeZoneHelper.ConvertIstToUtc(dateTime);

            var weather = await GetForecastBySpecificDate(city, requestedUtc);
            if (weather == null)
                return NotFound("Forecast not available for requested time.");

            return Ok(new CurrentWeatherResponse
            {
                City = city,
                Weather = weather
            });
        }

        // ================= PRIVATE METHODS =================

        private async Task<WeatherResult?> GetWeatherFromAPI(string city)
        {
            var client = _httpClientFactory.CreateClient();

            string? apiKey = _configuration["OpenWeather:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("OpenWeather API key is not configured.");

            string url =
                $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            return new WeatherResult
            {
                Temperature = root.GetProperty("main").GetProperty("temp").GetDouble(),
                Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32(),
                Condition = root.GetProperty("weather")[0].GetProperty("description").GetString()
            };
        }

        private async Task<ForecastResponse?> GetForecastFromAPI(string city)
        {
            var client = _httpClientFactory.CreateClient();

            string? apiKey = _configuration["OpenWeather:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("OpenWeather API key is not configured.");

            string url =
                $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = doc.RootElement.GetProperty("list");

            var forecasts = new List<WeatherResponse>();

            for (int i = 0; i < 3; i++)
            {
                var item = list[i];

                var utcTime = DateTime.SpecifyKind(
                    DateTime.Parse(item.GetProperty("dt_txt").GetString()!),
                    DateTimeKind.Utc);

                forecasts.Add(new WeatherResponse
                {
                    DateTimeIst = TimeZoneHelper.ConvertUtcToIst(utcTime),
                    Temperature = item.GetProperty("main").GetProperty("temp").GetDouble(),
                    Humidity = item.GetProperty("main").GetProperty("humidity").GetInt32(),
                    Condition = item.GetProperty("weather")[0].GetProperty("description").GetString()
                });
            }

            return new ForecastResponse
            {
                City = city,
                Forecasts = forecasts
            };
        }

        private async Task<WeatherResponse?> GetForecastBySpecificDate(string city, DateTime requestedUtc)
        {
            var forecast = await GetForecastFromAPI(city);
            if (forecast == null)
                return null;

            return forecast.Forecasts
                .OrderBy(f => Math.Abs((TimeZoneHelper.ConvertIstToUtc(f.DateTimeIst) - requestedUtc).TotalMinutes))
                .FirstOrDefault();
        }
    }
}
