namespace dsLoan.API.Utils
{
    public static class UtcNormalization
    {
        private static readonly TimeZoneInfo BelgradeTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");

        // Converts a date (or datetime) coming from query into UTC day-boundaries
        public static (DateTime? FromUtc, DateTime? ToUtcExclusive) NormalizeUtcDayRange(
            DateTime? fromDate,
            DateTime? toDate)
        {
            DateTime? fromUtc = null;
            DateTime? toUtcExclusive = null;

            if (fromDate.HasValue)
            {
                // treat query value as a BELGRADE calendar date (ignore time part)
                var localStart = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified);
                fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, BelgradeTz);
            }

            if (toDate.HasValue)
            {
                // exclusive end = next day at 00:00 local, converted to UTC
                var localEndExclusive = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
                toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, BelgradeTz);
            }

            return (fromUtc, toUtcExclusive);
        }
    }
}
