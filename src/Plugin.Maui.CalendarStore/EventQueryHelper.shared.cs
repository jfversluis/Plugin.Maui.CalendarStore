namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Helper methods for constructing calendar event date range queries.
/// </summary>
static partial class CalendarStore
{
	/// <summary>
	/// Computes the UTC timestamp in milliseconds for use in date range filters.
	/// </summary>
	/// <param name="date">The date to compute the filter timestamp for.</param>
	/// <param name="referenceOffset">Unused. Kept for source compatibility during transition.</param>
	/// <returns>The UTC timestamp in milliseconds since Unix epoch.</returns>
	internal static long ToFilterTimestampMillis(DateTimeOffset date, TimeSpan referenceOffset)
	{
		// DateTimeOffset.ToUnixTimeMilliseconds() already returns UTC-based epoch
		// milliseconds regardless of the offset, so no additional adjustment is needed.
		return date.ToUnixTimeMilliseconds();
	}
}
