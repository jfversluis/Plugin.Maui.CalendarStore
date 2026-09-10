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

	/// <summary>
	/// Resolves an IANA time zone identifier to a <see cref="TimeZoneInfo"/>.
	/// Falls back to the device's local time zone when the identifier is missing
	/// or unknown.
	/// </summary>
	/// <param name="timeZoneId">The IANA time zone identifier, or <see langword="null"/> for the local time zone.</param>
	internal static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
	{
		if (string.IsNullOrWhiteSpace(timeZoneId))
		{
			return TimeZoneInfo.Local;
		}

		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.Local;
		}
		catch (InvalidTimeZoneException)
		{
			return TimeZoneInfo.Local;
		}
	}

	/// <summary>
	/// Converts a UTC Unix timestamp in milliseconds to a <see cref="DateTimeOffset"/>
	/// expressed in the provided time zone, preserving the represented instant.
	/// </summary>
	/// <param name="milliseconds">The number of milliseconds since the Unix epoch (UTC).</param>
	/// <param name="timeZone">The time zone to express the result in.</param>
	internal static DateTimeOffset FromUnixTimeMilliseconds(long milliseconds, TimeZoneInfo timeZone)
	{
		var instant = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
		return TimeZoneInfo.ConvertTime(instant, timeZone);
	}

	/// <summary>
	/// Resolves the time zone an event should be anchored to.
	/// Returns <see langword="null"/> when no explicit time zone was provided,
	/// which signals that the device-local time zone (or the provided offset)
	/// should be used for backward compatibility.
	/// </summary>
	/// <param name="timeZoneId">The IANA time zone identifier, or <see langword="null"/>.</param>
	internal static TimeZoneInfo? ResolveEventTimeZone(string? timeZoneId) =>
		string.IsNullOrWhiteSpace(timeZoneId) ? null : ResolveTimeZone(timeZoneId);

	/// <summary>
	/// Resolves a wall-clock <see cref="DateTime"/> to a <see cref="DateTimeOffset"/>
	/// in the provided time zone. Wall times that fall in a daylight saving gap are
	/// moved forward to the first valid time.
	/// </summary>
	/// <param name="wallTime">The wall-clock date and time to resolve.</param>
	/// <param name="timeZone">The time zone to resolve the wall time in.</param>
	internal static DateTimeOffset ToWallTimeInstant(DateTime wallTime, TimeZoneInfo timeZone)
	{
		var unspecified = DateTime.SpecifyKind(wallTime, DateTimeKind.Unspecified);
		var attempts = 0;

		while (timeZone.IsInvalidTime(unspecified) && attempts < 180)
		{
			unspecified = unspecified.AddMinutes(1);
			attempts++;
		}

		return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified));
	}

	/// <summary>
	/// Resolves the instants an event should be stored with. When a time zone is
	/// supplied, the wall-clock component of the provided values is reinterpreted in
	/// that zone so recurring events keep their local time across daylight saving.
	/// All-day events are normalized to midnight (UTC).
	/// </summary>
	/// <param name="start">The requested start date and time.</param>
	/// <param name="end">The requested end date and time.</param>
	/// <param name="isAllDay">Whether the event is an all-day event.</param>
	/// <param name="timeZone">The event time zone, or <see langword="null"/> to use the provided instants.</param>
	internal static (DateTimeOffset Start, DateTimeOffset End) ResolveStoredTimes(
		DateTimeOffset start, DateTimeOffset end, bool isAllDay, TimeZoneInfo? timeZone)
	{
		if (isAllDay)
		{
			return (new DateTimeOffset(start.Date, TimeSpan.Zero),
				new DateTimeOffset(end.Date, TimeSpan.Zero));
		}

		if (timeZone is null)
		{
			return (start, end);
		}

		return (ToWallTimeInstant(start.DateTime, timeZone),
			ToWallTimeInstant(end.DateTime, timeZone));
	}
}
