namespace Thedeletr.Core;

public static class DurationFormatter
{
    public static string Format(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalSeconds < 60)
        {
            var seconds = (int)duration.TotalSeconds;
            return seconds == 1 ? "1 second" : $"{seconds} seconds";
        }

        if (duration.TotalMinutes < 60)
        {
            return $"{duration.Minutes:00}:{duration.Seconds:00}";
        }

        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
