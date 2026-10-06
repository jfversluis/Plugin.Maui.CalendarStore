using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Tests for the public RRULE round-tripping helpers on <see cref="CalendarRecurrence"/>.
/// </summary>
public class CalendarRecurrenceTests
{
	[Fact]
	public void ToRRule_SerializesToIcalendarValue()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Weekly,
			Interval = 2,
			DaysOfWeek = { new(DayOfWeek.Monday), new(DayOfWeek.Wednesday) },
		};

		Assert.Equal("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE", recurrence.ToRRule());
	}

	[Fact]
	public void ToRRule_Throws_ForInvalidRule()
	{
		var recurrence = new CalendarRecurrence
		{
			Count = 1,
			Until = DateTimeOffset.UtcNow,
		};

		Assert.Throws<InvalidOperationException>(() => recurrence.ToRRule());
	}

	[Fact]
	public void TryParse_RoundTripsWithToRRule()
	{
		const string source = "FREQ=MONTHLY;BYDAY=3TU,-1FR";

		Assert.True(CalendarRecurrence.TryParse(source, out var recurrence));
		Assert.NotNull(recurrence);
		Assert.Equal(RecurrenceFrequency.Monthly, recurrence.Frequency);
		Assert.Equal(3, recurrence.DaysOfWeek[0].WeekNumber);
		Assert.Equal(-1, recurrence.DaysOfWeek[1].WeekNumber);
		Assert.Equal(source, recurrence.ToRRule());
	}

	[Fact]
	public void TryParse_AcceptsPrefix()
	{
		Assert.True(CalendarRecurrence.TryParse("RRULE:FREQ=DAILY", out var recurrence));
		Assert.Equal(RecurrenceFrequency.Daily, recurrence.Frequency);
	}

	[Fact]
	public void TryParse_ReturnsFalse_ForUnsupportedValue()
	{
		Assert.False(CalendarRecurrence.TryParse("FREQ=HOURLY", out var recurrence));
		Assert.Null(recurrence);
	}

	[Fact]
	public void TryParse_ReturnsFalse_ForNullOrEmpty()
	{
		Assert.False(CalendarRecurrence.TryParse(null, out _));
		Assert.False(CalendarRecurrence.TryParse(string.Empty, out _));
	}

	[Fact]
	public void TryParse_WithTimeZone_InterpretsFloatingUntil()
	{
		var timeZone = TimeZoneInfo.CreateCustomTimeZone(
			"Tests/PlusTwo", TimeSpan.FromHours(2), "Tests +2", "Tests +2");

		Assert.True(CalendarRecurrence.TryParse(
			"FREQ=DAILY;UNTIL=20250101T100000", timeZone, out var recurrence));

		Assert.NotNull(recurrence!.Until);
		Assert.Equal(TimeSpan.FromHours(2), recurrence.Until!.Value.Offset);
	}

	[Fact]
	public void Parse_ReturnsRecurrence()
	{
		var recurrence = CalendarRecurrence.Parse("FREQ=YEARLY;BYMONTH=6");

		Assert.Equal(RecurrenceFrequency.Yearly, recurrence.Frequency);
		Assert.Equal(new[] { 6 }, recurrence.MonthsOfYear);
	}

	[Fact]
	public void Parse_Throws_ForInvalidValue()
	{
		Assert.Throws<FormatException>(() => CalendarRecurrence.Parse("not-an-rrule"));
	}
}
