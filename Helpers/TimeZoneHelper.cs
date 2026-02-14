using System;
using System.Runtime.InteropServices;

namespace WeatherBotAPI.Helpers
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo IstZone =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        public static DateTime ConvertIstToUtc(DateTime istTime)
        {
            var unspecified = DateTime.SpecifyKind(istTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, IstZone);
        }

        public static DateTime ConvertUtcToIst(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, IstZone);
        }
    }
}
