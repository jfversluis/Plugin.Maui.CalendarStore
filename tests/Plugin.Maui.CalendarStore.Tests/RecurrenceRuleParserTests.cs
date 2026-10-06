using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Tests for <see cref="RecurrenceRuleParser"/>: serialization and parsing of
/// iCalendar RRULE values and RFC 2445 durations, including time zone handling.
/// </summary>
public class RecurrenceRuleParserTests
{
	static readonly TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone(
		"Tests/PlusTwo", TimeSpan.FromHours(2), "Tests +2", "Tests +2");

	[Fact]
	public void ToRRule_SimpleDaily_OmitsDefaultInterval()
	{
		var recurrence = new CalendarRecurrence { Frequency = RecurrenceFrequency.Daily };

		Assert.Equal("FREQ=DAILY", RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_WeeklyWithDays_UsesUppercaseTokens()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Weekly,
			DaysOfWeek = { new(DayOfWeek.Monday), new(DayOfWeek.Wednesday), new(DayOfWeek.Friday) },
		};

		Assert.Equal("FREQ=WEEKLY;BYDAY=MO,WE,FR", RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_WithIntervalAndCount_EmitsBoth()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Weekly,
			Interval = 2,
			Count = 10,
			DaysOfWeek = { new(DayOfWeek.Monday) },
		};

		Assert.Equal("FREQ=WEEKLY;INTERVAL=2;COUNT=10;BYDAY=MO", RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_WithUntil_FormatsInUtc()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Daily,
			Until = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)),
		};

		Assert.Equal("FREQ=DAILY;UNTIL=20250101T100000Z", RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_OrdinalDays_PrefixesWeekNumber()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Monthly,
			DaysOfWeek = { new(DayOfWeek.Monday), new(DayOfWeek.Tuesday, 3), new(DayOfWeek.Friday, -1) },
		};

		Assert.Equal("FREQ=MONTHLY;BYDAY=MO,3TU,-1FR", RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_ComplexRule_EmitsAllPartsInOrder()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Yearly,
			FirstDayOfWeek = DayOfWeek.Sunday,
			DaysOfMonth = { 1, -1 },
			MonthsOfYear = { 1, 6 },
			DaysOfYear = { 100 },
			WeeksOfYear = { 20 },
			SetPositions = { -1 },
		};

		Assert.Equal(
			"FREQ=YEARLY;BYMONTHDAY=1,-1;BYMONTH=1,6;BYYEARDAY=100;BYWEEKNO=20;BYSETPOS=-1;WKST=SU",
			RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void ToRRule_Throws_WhenCountAndUntilBothSet()
	{
		var recurrence = new CalendarRecurrence
		{
			Count = 5,
			Until = DateTimeOffset.UtcNow,
		};

		Assert.Throws<InvalidOperationException>(() => RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void ToRRule_Throws_ForNonPositiveInterval(int interval)
	{
		var recurrence = new CalendarRecurrence { Interval = interval };

		Assert.Throws<InvalidOperationException>(() => RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Fact]
	public void TryParse_ReturnsFalse_ForNullOrEmpty()
	{
		Assert.False(RecurrenceRuleParser.TryParse(null, TimeZoneInfo.Utc, out _));
		Assert.False(RecurrenceRuleParser.TryParse("   ", TimeZoneInfo.Utc, out _));
	}

	[Fact]
	public void TryParse_AcceptsOptionalPrefix()
	{
		Assert.True(RecurrenceRuleParser.TryParse("RRULE:FREQ=WEEKLY;BYDAY=MO", TimeZoneInfo.Utc, out var recurrence));
		Assert.Equal(RecurrenceFrequency.Weekly, recurrence.Frequency);
		Assert.Single(recurrence.DaysOfWeek);
		Assert.Equal(DayOfWeek.Monday, recurrence.DaysOfWeek[0].Day);
		Assert.Null(recurrence.DaysOfWeek[0].WeekNumber);
	}

	[Fact]
	public void TryParse_ParsesOrdinalDays()
	{
		Assert.True(RecurrenceRuleParser.TryParse("FREQ=MONTHLY;BYDAY=3TU,-1FR", TimeZoneInfo.Utc, out var recurrence));

		Assert.Equal(2, recurrence.DaysOfWeek.Count);
		Assert.Equal(3, recurrence.DaysOfWeek[0].WeekNumber);
		Assert.Equal(DayOfWeek.Tuesday, recurrence.DaysOfWeek[0].Day);
		Assert.Equal(-1, recurrence.DaysOfWeek[1].WeekNumber);
		Assert.Equal(DayOfWeek.Friday, recurrence.DaysOfWeek[1].Day);
	}

	[Fact]
	public void TryParse_IgnoresUnknownParts()
	{
		Assert.True(RecurrenceRuleParser.TryParse(
			"FREQ=DAILY;X-VENDOR=SOMETHING;INTERVAL=3", TimeZoneInfo.Utc, out var recurrence));

		Assert.Equal(RecurrenceFrequency.Daily, recurrence.Frequency);
		Assert.Equal(3, recurrence.Interval);
	}

	[Fact]
	public void TryParse_ReturnsFalse_WhenBothCountAndUntil()
	{
		Assert.False(RecurrenceRuleParser.TryParse(
			"FREQ=DAILY;COUNT=5;UNTIL=20250101T000000Z", TimeZoneInfo.Utc, out _));
	}

	[Fact]
	public void TryParse_ReturnsFalse_WhenFrequencyMissing()
	{
		Assert.False(RecurrenceRuleParser.TryParse("INTERVAL=2;BYDAY=MO", TimeZoneInfo.Utc, out _));
	}

	[Theory]
	[InlineData("FREQ=MONTHLY;BYMONTHDAY=32")]
	[InlineData("FREQ=MONTHLY;BYMONTHDAY=-32")]
	[InlineData("FREQ=YEARLY;BYMONTH=13")]
	[InlineData("FREQ=YEARLY;BYMONTH=-1")]
	[InlineData("FREQ=YEARLY;BYYEARDAY=367")]
	[InlineData("FREQ=YEARLY;BYWEEKNO=-54")]
	[InlineData("FREQ=MONTHLY;BYSETPOS=367")]
	[InlineData("FREQ=MONTHLY;BYDAY=54MO")]
	[InlineData("FREQ=MONTHLY;BYDAY=-54MO")]
	public void TryParse_ReturnsFalse_ForOutOfRangeComponents(string value)
	{
		Assert.False(RecurrenceRuleParser.TryParse(value, TimeZoneInfo.Utc, out _));
	}

	[Fact]
	public void ToRRule_Throws_ForOutOfRangeComponents()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Yearly,
			MonthsOfYear = { 13 },
		};

		Assert.Throws<InvalidOperationException>(() => recurrence.ToRRule());
	}

	[Fact]
	public void ToRRule_Throws_ForOutOfRangeDayOrdinal()
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Monthly,
			DaysOfWeek = { new(DayOfWeek.Monday, 54) },
		};

		Assert.Throws<InvalidOperationException>(() => recurrence.ToRRule());
	}

	[Theory]
	[InlineData("FREQ=HOURLY")]
	[InlineData("FREQ=SECONDLY")]
	[InlineData("FREQ=MINUTELY")]
	public void TryParse_ReturnsFalse_ForUnsupportedFrequency(string value)
	{
		Assert.False(RecurrenceRuleParser.TryParse(value, TimeZoneInfo.Utc, out _));
	}

	[Fact]
	public void TryParse_UntilWithZ_UsesUtcOffset()
	{
		Assert.True(RecurrenceRuleParser.TryParse(
			"FREQ=DAILY;UNTIL=20250101T100000Z", plusTwo, out var recurrence));

		Assert.NotNull(recurrence.Until);
		Assert.Equal(TimeSpan.Zero, recurrence.Until!.Value.Offset);
		Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc), recurrence.Until.Value.UtcDateTime);
	}

	[Fact]
	public void TryParse_UntilFloating_UsesProvidedTimeZone()
	{
		Assert.True(RecurrenceRuleParser.TryParse(
			"FREQ=DAILY;UNTIL=20250101T100000", plusTwo, out var recurrence));

		Assert.NotNull(recurrence.Until);
		Assert.Equal(TimeSpan.FromHours(2), recurrence.Until!.Value.Offset);
		Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0), recurrence.Until.Value.DateTime);
	}

	[Fact]
	public void TryParse_UntilDateOnly_UsesMidnightInTimeZone()
	{
		Assert.True(RecurrenceRuleParser.TryParse(
			"FREQ=DAILY;UNTIL=20250101", plusTwo, out var recurrence));

		Assert.NotNull(recurrence.Until);
		Assert.Equal(TimeSpan.FromHours(2), recurrence.Until!.Value.Offset);
		Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0), recurrence.Until.Value.DateTime);
	}

	[Fact]
	public void TryParse_ThenToRRule_RoundTrips()
	{
		const string source = "FREQ=WEEKLY;INTERVAL=2;COUNT=10;BYDAY=MO,WE;WKST=SU";

		Assert.True(RecurrenceRuleParser.TryParse(source, TimeZoneInfo.Utc, out var recurrence));

		Assert.Equal(source, RecurrenceRuleParser.ToRRule(recurrence));
	}

	[Theory]
	[InlineData(0, 1, 0, 0, "PT1H")]
	[InlineData(1, 0, 0, 0, "P1D")]
	[InlineData(7, 0, 0, 0, "P1W")]
	[InlineData(14, 0, 0, 0, "P2W")]
	[InlineData(0, 16, 0, 0, "PT16H")]
	[InlineData(1, 2, 0, 0, "P1DT2H")]
	[InlineData(0, 0, 90, 0, "PT1H30M")]
	[InlineData(0, 0, 0, 90, "PT1M30S")]
	[InlineData(1, 0, 0, 30, "P1DT30S")]
	public void FormatDuration_ProducesExpectedValue(int days, int hours, int minutes, int seconds, string expected)
	{
		var duration = TimeSpan.FromDays(days)
			+ TimeSpan.FromHours(hours)
			+ TimeSpan.FromMinutes(minutes)
			+ TimeSpan.FromSeconds(seconds);

		Assert.Equal(expected, RecurrenceRuleParser.FormatDuration(duration));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(0.5)]
	public void FormatDuration_Throws_ForInvalidDuration(double totalSeconds)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			RecurrenceRuleParser.FormatDuration(TimeSpan.FromSeconds(totalSeconds)));
	}

	[Theory]
	[InlineData("PT1H", 3600)]
	[InlineData("P1D", 86400)]
	[InlineData("P1W", 604800)]
	[InlineData("P2W", 1209600)]
	[InlineData("P1DT2H", 93600)]
	[InlineData("PT1H30M", 5400)]
	[InlineData("PT1M30S", 90)]
	public void TryParseDuration_ParsesValidValue(string value, double expectedSeconds)
	{
		Assert.True(RecurrenceRuleParser.TryParseDuration(value, out var duration));

		Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
	}

	[Theory]
	[InlineData("")]
	[InlineData("P")]
	[InlineData("PT")]
	[InlineData("1H")]
	[InlineData("P1H")]
	[InlineData("garbage")]
	public void TryParseDuration_ReturnsFalse_ForInvalidValue(string value)
	{
		Assert.False(RecurrenceRuleParser.TryParseDuration(value, out _));
	}

	[Fact]
	public void Duration_RoundTripsThroughText()
	{
		var original = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30);

		var text = RecurrenceRuleParser.FormatDuration(original);

		Assert.True(RecurrenceRuleParser.TryParseDuration(text, out var parsed));
		Assert.Equal(original, parsed);
	}

	[Fact]
	public void ResolveTimeZone_Null_ReturnsLocal()
	{
		Assert.Equal(TimeZoneInfo.Local, CalendarStore.ResolveTimeZone(null));
	}

	[Fact]
	public void ResolveTimeZone_Unknown_ReturnsLocal()
	{
		Assert.Equal(TimeZoneInfo.Local, CalendarStore.ResolveTimeZone("Not/ARealZone"));
	}

	[Fact]
	public void ResolveTimeZone_Known_ReturnsMatchingZone()
	{
		var resolved = CalendarStore.ResolveTimeZone(plusTwo.Id);

		Assert.Equal(TimeSpan.FromHours(2), resolved.BaseUtcOffset);
	}

	[Fact]
	public void FromUnixTimeMilliseconds_PreservesInstant()
	{
		var instant = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
		var millis = instant.ToUnixTimeMilliseconds();

		var zoned = CalendarStore.FromUnixTimeMilliseconds(millis, plusTwo);

		Assert.Equal(instant, zoned.ToUniversalTime());
		Assert.Equal(TimeSpan.FromHours(2), zoned.Offset);
		Assert.Equal(new DateTime(2025, 6, 15, 14, 0, 0), zoned.DateTime);
	}
}
