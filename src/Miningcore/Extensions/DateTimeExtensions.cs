using System;

namespace Miningcore.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static double ToUnixTimestamp(this DateTime value)
        {
            var span = value.ToUniversalTime() - UnixEpoch;
            return span.TotalSeconds;
        }

        public static DateTime FromUnixTimestamp(this double timestamp)
        {
            return UnixEpoch.AddSeconds(timestamp);
        }
    }
}