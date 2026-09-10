using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Converts between the cross-platform <see cref="CalendarRecurrence"/> model and
/// iCalendar (RFC 5545) <c>RRULE</c> values, and parses RFC 2445 durations.
/// </summary>
internal static partial class RecurrenceRuleParser
{
	const string rrulePrefix = "RRULE:";

	internal static void Validate(CalendarRecurrence recurrence)
	{
		ArgumentNullException.ThrowIfNull(recurrence);

		if (recurrence.Count is not null && recurrence.Until is not null)
		{
			throw new InvalidOperationException("A recurrence rule cannot specify both Count and Until.");
		}

		if (recurrence.Interval < 1)
		{
			throw new InvalidOperationException("The recurrence interval must be greater than zero.");
		}

		if (recurrence.Count is <= 0)
		{
			throw new InvalidOperationException("The recurrence count must be greater than zero.");
		}

		_ = FrequencyToken(recurrence.Frequency);

		ValidateRange(recurrence.DaysOfMonth, 1, 31, nameof(recurrence.DaysOfMonth), true);
		ValidateRange(recurrence.MonthsOfYear, 1, 12, nameof(recurrence.MonthsOfYear), false);
		ValidateRange(recurrence.WeeksOfYear, 1, 53, nameof(recurrence.WeeksOfYear), true);
		ValidateRange(recurrence.DaysOfYear, 1, 366, nameof(recurrence.DaysOfYear), true);
		ValidateRange(recurrence.SetPositions, 1, 366, nameof(recurrence.SetPositions), true);

		foreach (var day in recurrence.DaysOfWeek)
		{
			if (day is null)
			{
				throw new InvalidOperationException("DaysOfWeek cannot contain null values.");
			}

			_ = DayAbbreviation(day.Day);

			if (day.WeekNumber is int weekNumber && (weekNumber == 0 || Math.Abs((long)weekNumber) > 53))
			{
				throw new InvalidOperationException(
					"A recurrence day ordinal must be between -53 and 53 and cannot be zero.");
			}
		}
	}

	[GeneratedRegex(
		@"^P(?:(?<weeks>\d+)W|(?:(?<days>\d+)D)?(?:T(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+)S)?)?)$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex DurationRegex();

	/// <summary>
	/// Serializes the provided recurrence rule to an iCalendar <c>RRULE</c> value
	/// (without the <c>RRULE:</c> prefix).
	/// </summary>
	internal static string ToRRule(CalendarRecurrence recurrence)
	{
		Validate(recurrence);

		var parts = new List<string> { $"FREQ={FrequencyToken(recurrence.Frequency)}" };

		if (recurrence.Interval != 1)
		{
			parts.Add($"INTERVAL={recurrence.Interval.ToString(CultureInfo.InvariantCulture)}");
		}

		if (recurrence.Count is int count)
		{
			parts.Add($"COUNT={count.ToString(CultureInfo.InvariantCulture)}");
		}
		else if (recurrence.Until is DateTimeOffset until)
		{
			parts.Add($"UNTIL={FormatUntilUtc(until)}");
		}

		if (recurrence.DaysOfWeek.Count > 0)
		{
			parts.Add($"BYDAY={string.Join(",", recurrence.DaysOfWeek.Select(FormatDayOfWeek))}");
		}

		if (recurrence.DaysOfMonth.Count > 0)
		{
			parts.Add($"BYMONTHDAY={JoinIntegers(recurrence.DaysOfMonth)}");
		}

		if (recurrence.MonthsOfYear.Count > 0)
		{
			parts.Add($"BYMONTH={JoinIntegers(recurrence.MonthsOfYear)}");
		}

		if (recurrence.DaysOfYear.Count > 0)
		{
			parts.Add($"BYYEARDAY={JoinIntegers(recurrence.DaysOfYear)}");
		}

		if (recurrence.WeeksOfYear.Count > 0)
		{
			parts.Add($"BYWEEKNO={JoinIntegers(recurrence.WeeksOfYear)}");
		}

		if (recurrence.SetPositions.Count > 0)
		{
			parts.Add($"BYSETPOS={JoinIntegers(recurrence.SetPositions)}");
		}

		if (recurrence.FirstDayOfWeek is DayOfWeek firstDay)
		{
			parts.Add($"WKST={DayAbbreviation(firstDay)}");
		}

		return string.Join(";", parts);
	}

	/// <summary>
	/// Parses an iCalendar <c>RRULE</c> value. An optional <c>RRULE:</c> prefix is
	/// accepted. Values that are not part of the supported set are treated as floating
	/// and interpreted in <paramref name="timeZone"/>.
	/// </summary>
	internal static bool TryParse(string? raw, TimeZoneInfo timeZone, out CalendarRecurrence recurrence)
	{
		recurrence = new CalendarRecurrence();

		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		timeZone ??= TimeZoneInfo.Utc;

		var value = raw.Trim();

		if (value.StartsWith(rrulePrefix, StringComparison.OrdinalIgnoreCase))
		{
			value = value[rrulePrefix.Length..];
		}

		var parsed = new CalendarRecurrence();
		var hasFrequency = false;
		var hasCount = false;
		var hasUntil = false;

		foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var separator = segment.IndexOf('=');

			if (separator <= 0)
			{
				return false;
			}

			var key = segment[..separator].Trim().ToUpperInvariant();
			var segmentValue = segment[(separator + 1)..].Trim();

			switch (key)
			{
				case "FREQ":
					if (!TryParseFrequency(segmentValue, out var frequency))
					{
						return false;
					}

					parsed.Frequency = frequency;
					hasFrequency = true;
					break;
				case "INTERVAL":
					if (!TryParsePositiveInt(segmentValue, out var interval))
					{
						return false;
					}

					parsed.Interval = interval;
					break;
				case "COUNT":
					if (!TryParsePositiveInt(segmentValue, out var count))
					{
						return false;
					}

					parsed.Count = count;
					hasCount = true;
					break;
				case "UNTIL":
					if (!TryParseUntil(segmentValue, timeZone, out var until))
					{
						return false;
					}

					parsed.Until = until;
					hasUntil = true;
					break;
				case "BYDAY":
					if (!TryParseDaysOfWeek(segmentValue, parsed.DaysOfWeek))
					{
						return false;
					}

					break;
				case "BYMONTHDAY":
					if (!TryParseIntegerList(segmentValue, parsed.DaysOfMonth, 1, 31, true))
					{
						return false;
					}

					break;
				case "BYMONTH":
					if (!TryParseIntegerList(segmentValue, parsed.MonthsOfYear, 1, 12, false))
					{
						return false;
					}

					break;
				case "BYYEARDAY":
					if (!TryParseIntegerList(segmentValue, parsed.DaysOfYear, 1, 366, true))
					{
						return false;
					}

					break;
				case "BYWEEKNO":
					if (!TryParseIntegerList(segmentValue, parsed.WeeksOfYear, 1, 53, true))
					{
						return false;
					}

					break;
				case "BYSETPOS":
					if (!TryParseIntegerList(segmentValue, parsed.SetPositions, 1, 366, true))
					{
						return false;
					}

					break;
				case "WKST":
					if (!TryMapDay(segmentValue, out var firstDay))
					{
						return false;
					}

					parsed.FirstDayOfWeek = firstDay;
					break;
				default:
					// Ignore unknown parts for forward compatibility.
					break;
			}
		}

		if (!hasFrequency || (hasCount && hasUntil))
		{
			return false;
		}

		recurrence = parsed;
		return true;
	}

	/// <summary>
	/// Formats a <see cref="TimeSpan"/> as an RFC 2445 duration, for example
	/// <c>PT1H</c>, <c>P1D</c> or <c>P1W</c>.
	/// </summary>
	internal static string FormatDuration(TimeSpan duration)
	{
		if (duration <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(duration), "The duration must be greater than zero.");
		}

		if (duration.Ticks % TimeSpan.TicksPerSecond != 0)
		{
			throw new ArgumentOutOfRangeException(nameof(duration), "The duration cannot contain fractional seconds.");
		}

		if (duration.Ticks % (TimeSpan.TicksPerDay * 7) == 0)
		{
			return $"P{duration.Days / 7}W";
		}

		var builder = new StringBuilder("P");

		if (duration.Days > 0)
		{
			builder.Append(duration.Days.ToString(CultureInfo.InvariantCulture)).Append('D');
		}

		if (duration.Hours > 0 || duration.Minutes > 0 || duration.Seconds > 0)
		{
			builder.Append('T');

			if (duration.Hours > 0)
			{
				builder.Append(duration.Hours.ToString(CultureInfo.InvariantCulture)).Append('H');
			}

			if (duration.Minutes > 0)
			{
				builder.Append(duration.Minutes.ToString(CultureInfo.InvariantCulture)).Append('M');
			}

			if (duration.Seconds > 0)
			{
				builder.Append(duration.Seconds.ToString(CultureInfo.InvariantCulture)).Append('S');
			}
		}

		return builder.ToString();
	}

	/// <summary>
	/// Parses an RFC 2445 duration into a <see cref="TimeSpan"/>.
	/// </summary>
	internal static bool TryParseDuration(string? raw, out TimeSpan duration)
	{
		duration = default;

		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		var match = DurationRegex().Match(raw.Trim());

		if (!match.Success)
		{
			return false;
		}

		var hasValue = false;

		var weeks = ReadGroup(match, "weeks", ref hasValue);
		var days = ReadGroup(match, "days", ref hasValue);
		var hours = ReadGroup(match, "hours", ref hasValue);
		var minutes = ReadGroup(match, "minutes", ref hasValue);
		var seconds = ReadGroup(match, "seconds", ref hasValue);

		if (!hasValue)
		{
			return false;
		}

		duration = TimeSpan.FromDays((weeks * 7) + days)
			+ TimeSpan.FromHours(hours)
			+ TimeSpan.FromMinutes(minutes)
			+ TimeSpan.FromSeconds(seconds);

		return duration > TimeSpan.Zero;
	}

	/// <summary>
	/// Formats a date and time as a UTC iCalendar date-time (<c>yyyyMMddTHHmmssZ</c>).
	/// </summary>
	internal static string FormatUntilUtc(DateTimeOffset until) =>
		until.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses an iCalendar <c>UNTIL</c> value. Values without a <c>Z</c> suffix are
	/// treated as wall-clock time in <paramref name="timeZone"/>.
	/// </summary>
	internal static bool TryParseUntil(string? raw, TimeZoneInfo timeZone, out DateTimeOffset until)
	{
		until = default;

		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		timeZone ??= TimeZoneInfo.Utc;

		var value = raw.Trim();

		if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
		{
			if (!DateTime.TryParseExact(value[..^1], "yyyyMMdd'T'HHmmss",
				CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc))
			{
				return false;
			}

			until = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
			return true;
		}

		if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd",
			CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
		{
			until = FromWallTime(dateOnly, timeZone);
			return true;
		}

		if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss",
			CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
		{
			until = FromWallTime(dateTime, timeZone);
			return true;
		}

		return false;
	}

	static DateTimeOffset FromWallTime(DateTime wallTime, TimeZoneInfo timeZone) =>
		CalendarStore.ToWallTimeInstant(wallTime, timeZone);

	static bool TryParseFrequency(string value, out RecurrenceFrequency frequency)
	{
		switch (value.Trim().ToUpperInvariant())
		{
			case "DAILY":
				frequency = RecurrenceFrequency.Daily;
				return true;
			case "WEEKLY":
				frequency = RecurrenceFrequency.Weekly;
				return true;
			case "MONTHLY":
				frequency = RecurrenceFrequency.Monthly;
				return true;
			case "YEARLY":
				frequency = RecurrenceFrequency.Yearly;
				return true;
			default:
				frequency = default;
				return false;
		}
	}

	static string FrequencyToken(RecurrenceFrequency frequency) =>
		frequency switch
		{
			RecurrenceFrequency.Daily => "DAILY",
			RecurrenceFrequency.Weekly => "WEEKLY",
			RecurrenceFrequency.Monthly => "MONTHLY",
			RecurrenceFrequency.Yearly => "YEARLY",
			_ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
				"Unknown recurrence frequency."),
		};

	static bool TryParseDaysOfWeek(string value, IList<RecurrenceDayOfWeek> target)
	{
		foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var entry = token.Trim();

			if (entry.Length < 2)
			{
				return false;
			}

			if (!TryMapDay(entry[^2..], out var day))
			{
				return false;
			}

			int? weekNumber = null;
			var ordinal = entry[..^2];

			if (ordinal.Length > 0)
			{
				if (!int.TryParse(ordinal, NumberStyles.AllowLeadingSign,
					CultureInfo.InvariantCulture, out var parsedOrdinal)
					|| parsedOrdinal == 0 || Math.Abs((long)parsedOrdinal) > 53)
				{
					return false;
				}

				weekNumber = parsedOrdinal;
			}

			target.Add(new RecurrenceDayOfWeek(day, weekNumber));
		}

		return target.Count > 0;
	}

	static bool TryParseIntegerList(string value, IList<int> target, int minimum,
		int maximum, bool allowNegative)
	{
		foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			if (!int.TryParse(token.Trim(), NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out var parsed)
				|| !IsInRange(parsed, minimum, maximum, allowNegative))
			{
				return false;
			}

			target.Add(parsed);
		}

		return target.Count > 0;
	}

	static void ValidateRange(IEnumerable<int> values, int minimum, int maximum,
		string propertyName, bool allowNegative)
	{
		foreach (var value in values)
		{
			if (!IsInRange(value, minimum, maximum, allowNegative))
			{
				var negativeDescription = allowNegative ? $" or -{maximum} through -{minimum}" : string.Empty;
				throw new InvalidOperationException(
					$"{propertyName} values must be between {minimum} and {maximum}{negativeDescription}.");
			}
		}
	}

	static bool IsInRange(int value, int minimum, int maximum, bool allowNegative)
	{
		var magnitude = Math.Abs((long)value);
		return value != 0 && magnitude >= minimum && magnitude <= maximum
			&& (allowNegative || value > 0);
	}

	static bool TryParsePositiveInt(string value, out int result) =>
		int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;

	static bool TryMapDay(string token, out DayOfWeek day)
	{
		switch (token.Trim().ToUpperInvariant())
		{
			case "SU":
				day = DayOfWeek.Sunday;
				return true;
			case "MO":
				day = DayOfWeek.Monday;
				return true;
			case "TU":
				day = DayOfWeek.Tuesday;
				return true;
			case "WE":
				day = DayOfWeek.Wednesday;
				return true;
			case "TH":
				day = DayOfWeek.Thursday;
				return true;
			case "FR":
				day = DayOfWeek.Friday;
				return true;
			case "SA":
				day = DayOfWeek.Saturday;
				return true;
			default:
				day = default;
				return false;
		}
	}

	static string DayAbbreviation(DayOfWeek day) =>
		day switch
		{
			DayOfWeek.Sunday => "SU",
			DayOfWeek.Monday => "MO",
			DayOfWeek.Tuesday => "TU",
			DayOfWeek.Wednesday => "WE",
			DayOfWeek.Thursday => "TH",
			DayOfWeek.Friday => "FR",
			DayOfWeek.Saturday => "SA",
			_ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown day of the week."),
		};

	static string FormatDayOfWeek(RecurrenceDayOfWeek dayOfWeek) =>
		dayOfWeek.WeekNumber is int weekNumber
			? $"{weekNumber.ToString(CultureInfo.InvariantCulture)}{DayAbbreviation(dayOfWeek.Day)}"
			: DayAbbreviation(dayOfWeek.Day);

	static string JoinIntegers(IEnumerable<int> values) =>
		string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));

	static double ReadGroup(Match match, string groupName, ref bool hasValue)
	{
		var group = match.Groups[groupName];

		if (!group.Success)
		{
			return 0;
		}

		hasValue = true;
		return double.Parse(group.Value, CultureInfo.InvariantCulture);
	}
}
