using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Specifies how often a <see cref="CalendarRecurrence"/> repeats.
/// </summary>
public enum RecurrenceFrequency
{
	/// <summary>The event repeats every day.</summary>
	Daily,

	/// <summary>The event repeats every week.</summary>
	Weekly,

	/// <summary>The event repeats every month.</summary>
	Monthly,

	/// <summary>The event repeats every year.</summary>
	Yearly,
}

/// <summary>
/// Represents a day of the week on which a recurrence rule applies, optionally
/// with an ordinal to indicate which occurrence within the frequency period,
/// for example the third Tuesday (<c>3TU</c>) or the last Friday (<c>-1FR</c>).
/// </summary>
public class RecurrenceDayOfWeek
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RecurrenceDayOfWeek"/> class.
	/// </summary>
	public RecurrenceDayOfWeek()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RecurrenceDayOfWeek"/> class.
	/// </summary>
	/// <param name="day">The day of the week.</param>
	/// <param name="weekNumber">
	/// The ordinal occurrence within the frequency period, for example <c>3</c> for the
	/// third occurrence or <c>-1</c> for the last. <see langword="null"/> means every occurrence.
	/// </param>
	public RecurrenceDayOfWeek(DayOfWeek day, int? weekNumber = null)
	{
		Day = day;
		WeekNumber = weekNumber;
	}

	/// <summary>
	/// Gets or sets the day of the week.
	/// </summary>
	public DayOfWeek Day { get; set; }

	/// <summary>
	/// Gets or sets the ordinal occurrence within the frequency period.
	/// </summary>
	/// <remarks>
	/// For example, <c>3</c> for the third occurrence or <c>-1</c> for the last.
	/// <see langword="null"/> means every occurrence of <see cref="Day"/>.
	/// </remarks>
	public int? WeekNumber { get; set; }
}

/// <summary>
/// Describes the pattern for a recurring <see cref="CalendarEvent"/>.
/// </summary>
/// <remarks>
/// This is a cross-platform representation of an iCalendar (RFC 5545) recurrence
/// rule. On iOS and macOS it is mapped to <c>EKRecurrenceRule</c>, on Android it
/// is serialized to the <c>RRULE</c> column.
/// </remarks>
public class CalendarRecurrence
{
	/// <summary>
	/// Gets or sets how often the event repeats.
	/// </summary>
	public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Daily;

	/// <summary>
	/// Gets or sets the interval between occurrences.
	/// </summary>
	/// <remarks>For example, a value of <c>2</c> with a weekly frequency means every other week. Defaults to 1.</remarks>
	public int Interval { get; set; } = 1;

	/// <summary>
	/// Gets or sets the total number of occurrences.
	/// </summary>
	/// <remarks>
	/// Mutually exclusive with <see cref="Until"/>. When both are <see langword="null"/>
	/// the recurrence never ends.
	/// </remarks>
	public int? Count { get; set; }

	/// <summary>
	/// Gets or sets the date and time after which the recurrence ends.
	/// </summary>
	/// <remarks>
	/// Mutually exclusive with <see cref="Count"/>. The value is inclusive and is
	/// stored in UTC when serialized to an iCalendar rule.
	/// </remarks>
	public DateTimeOffset? Until { get; set; }

	/// <summary>
	/// Gets or sets the day that is treated as the first day of the week.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> means the iCalendar default (Monday). Only relevant
	/// for rules with a weekly frequency and an interval greater than 1.
	/// </remarks>
	public DayOfWeek? FirstDayOfWeek { get; set; }

	/// <summary>
	/// Gets the days of the week on which the event repeats.
	/// </summary>
	public IList<RecurrenceDayOfWeek> DaysOfWeek { get; } = [];

	/// <summary>
	/// Gets the days of the month on which the event repeats.
	/// </summary>
	/// <remarks>Values range from 1 to 31. Negative values count from the end of the month, where -1 is the last day.</remarks>
	public IList<int> DaysOfMonth { get; } = [];

	/// <summary>
	/// Gets the months of the year on which the event repeats.
	/// </summary>
	/// <remarks>Values range from 1 to 12.</remarks>
	public IList<int> MonthsOfYear { get; } = [];

	/// <summary>
	/// Gets the weeks of the year on which the event repeats.
	/// </summary>
	/// <remarks>Values range from 1 to 53. Negative values count from the end of the year.</remarks>
	public IList<int> WeeksOfYear { get; } = [];

	/// <summary>
	/// Gets the days of the year on which the event repeats.
	/// </summary>
	/// <remarks>Values range from 1 to 366. Negative values count from the end of the year.</remarks>
	public IList<int> DaysOfYear { get; } = [];

	/// <summary>
	/// Gets the ordinal positions that filter the set of occurrences within the frequency period.
	/// </summary>
	/// <remarks>For example, <c>-1</c> selects the last occurrence of the period.</remarks>
	public IList<int> SetPositions { get; } = [];

	/// <summary>
	/// Serializes this recurrence to an iCalendar (RFC 5545) <c>RRULE</c> value, without the
	/// <c>RRULE:</c> prefix.
	/// </summary>
	/// <returns>The <c>RRULE</c> value, for example <c>FREQ=WEEKLY;BYDAY=MO,WE</c>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the rule is not valid, for example when both <see cref="Count"/> and
	/// <see cref="Until"/> are set, or <see cref="Interval"/> is less than 1.
	/// </exception>
	public string ToRRule() => RecurrenceRuleParser.ToRRule(this);

	/// <summary>
	/// Tries to parse an iCalendar (RFC 5545) <c>RRULE</c> value.
	/// </summary>
	/// <param name="rrule">The <c>RRULE</c> value. An optional <c>RRULE:</c> prefix is accepted.</param>
	/// <param name="timeZone">
	/// The time zone used to interpret an <c>UNTIL</c> value that is not in UTC, or
	/// <see langword="null"/> to use the device's local time zone.
	/// </param>
	/// <param name="recurrence">
	/// When this method returns <see langword="true"/>, contains the parsed recurrence;
	/// otherwise <see langword="null"/>.
	/// </param>
	/// <returns><see langword="true"/> if the value could be parsed; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(string? rrule, TimeZoneInfo? timeZone,
		[NotNullWhen(true)] out CalendarRecurrence? recurrence)
	{
		if (RecurrenceRuleParser.TryParse(rrule, timeZone ?? TimeZoneInfo.Local, out var parsed))
		{
			recurrence = parsed;
			return true;
		}

		recurrence = null;
		return false;
	}

	/// <summary>
	/// Tries to parse an iCalendar (RFC 5545) <c>RRULE</c> value, interpreting non-UTC
	/// <c>UNTIL</c> values in the device's local time zone.
	/// </summary>
	/// <param name="rrule">The <c>RRULE</c> value. An optional <c>RRULE:</c> prefix is accepted.</param>
	/// <param name="recurrence">
	/// When this method returns <see langword="true"/>, contains the parsed recurrence;
	/// otherwise <see langword="null"/>.
	/// </param>
	/// <returns><see langword="true"/> if the value could be parsed; otherwise <see langword="false"/>.</returns>
	public static bool TryParse(string? rrule, [NotNullWhen(true)] out CalendarRecurrence? recurrence) =>
		TryParse(rrule, null, out recurrence);

	/// <summary>
	/// Parses an iCalendar (RFC 5545) <c>RRULE</c> value.
	/// </summary>
	/// <param name="rrule">The <c>RRULE</c> value. An optional <c>RRULE:</c> prefix is accepted.</param>
	/// <param name="timeZone">
	/// The time zone used to interpret an <c>UNTIL</c> value that is not in UTC, or
	/// <see langword="null"/> to use the device's local time zone.
	/// </param>
	/// <returns>The parsed recurrence.</returns>
	/// <exception cref="FormatException">Thrown when <paramref name="rrule"/> is not a valid, supported rule.</exception>
	public static CalendarRecurrence Parse(string rrule, TimeZoneInfo? timeZone = null) =>
		TryParse(rrule, timeZone, out var recurrence)
			? recurrence
			: throw new FormatException($"'{rrule}' is not a valid iCalendar RRULE value.");
}

/// <summary>
/// Specifies which occurrences of a recurring event an operation applies to.
/// </summary>
public enum RecurrenceScope
{
	/// <summary>Apply the operation to a single occurrence.</summary>
	ThisEvent,

	/// <summary>Apply the operation to the entire series.</summary>
	AllEvents,
}
