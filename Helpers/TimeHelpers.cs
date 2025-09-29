namespace Freelancing.Helpers
{
    public static class TimeHelpers
    {
        public static DateTime ToPhilippineTime(DateTime utcDateTime)
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), phTimeZone);
        }
    }
}