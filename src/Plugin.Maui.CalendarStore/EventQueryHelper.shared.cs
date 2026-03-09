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
	/// <returns>The UTC timestamp in milliseconds since Unix epoch.</returns>
	internal static long ToFilterTimestampMillis(DateTimeOffset date)
	{
		// DateTimeOffset.ToUnixTimeMilliseconds() already returns UTC-based epoch
		// milliseconds regardless of the offset, so no additional adjustment is needed.
		return date.ToUnixTimeMilliseconds();
	}

	/// <summary>
	/// Builds the SQL selection clause for filtering events in an Instances query.
	/// Always excludes soft-deleted events and optionally filters by calendar ID.
	/// </summary>
	/// <param name="deletedColumn">The column name for the deleted flag.</param>
	/// <param name="calendarIdColumn">The column name for the calendar ID.</param>
	/// <param name="calendarId">Optional calendar ID to filter by.</param>
	/// <returns>A SQL selection string.</returns>
	internal static string BuildInstancesSelection(string deletedColumn,
		string calendarIdColumn, string? calendarId = null)
	{
		var selection = $"{deletedColumn} != 1";
		if (!string.IsNullOrEmpty(calendarId))
		{
			selection += $" AND {calendarIdColumn} = {calendarId}";
		}
		return selection;
	}

	/// <summary>
	/// Retrieves a value from a cache, populating it via a factory on cache miss.
	/// Used to avoid redundant queries for recurring event occurrences.
	/// </summary>
	internal static TValue GetOrAdd<TKey, TValue>(Dictionary<TKey, TValue> cache,
		TKey key, Func<TKey, TValue> factory) where TKey : notnull
	{
		if (!cache.TryGetValue(key, out var value))
		{
			value = factory(key);
			cache[key] = value;
		}
		return value;
	}
}
