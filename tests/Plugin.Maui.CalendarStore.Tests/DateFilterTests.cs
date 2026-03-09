using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Tests for Android date range filter timestamp computation.
/// Validates that UTC timestamps are computed correctly for calendar queries.
/// See issue #51: stale event data caused partly by timezone double-counting.
/// </summary>
public class DateFilterTests
{
	[Theory]
	[InlineData(5)]
	[InlineData(-5)]
	[InlineData(0)]
	[InlineData(5.5)]  // India (UTC+5:30)
	[InlineData(-9.5)] // Marquesas Islands
	public void ToFilterTimestampMillis_ShouldNotShiftByTimezoneOffset(double offsetHours)
	{
		// Arrange: a date in a non-UTC timezone
		var offset = TimeSpan.FromHours(offsetHours);
		var date = new DateTimeOffset(2025, 1, 15, 10, 0, 0, offset);

		// The correct UTC millis — DateTimeOffset.ToUnixTimeMilliseconds() is
		// already timezone-independent, so the filter value should equal it exactly.
		var expectedUtcMillis = date.ToUnixTimeMilliseconds();

		// Act
		var actual = CalendarStore.ToFilterTimestampMillis(date, date.Offset);

		// Assert — this FAILS with the current implementation because
		// it adds the offset a second time, shifting the result.
		Assert.Equal(expectedUtcMillis, actual);
	}

	[Fact]
	public void ToFilterTimestampMillis_EndDate_ShouldUseOwnOffset_NotStartDateOffset()
	{
		// Arrange: start and end dates with different offsets (e.g. DST transition)
		var startDate = new DateTimeOffset(2025, 3, 8, 23, 0, 0, TimeSpan.FromHours(-5)); // EST
		var endDate = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.FromHours(-4));  // EDT

		var expectedEndUtcMillis = endDate.ToUnixTimeMilliseconds();

		// Act — current code passes startDate.Offset for the endDate too
		var actual = CalendarStore.ToFilterTimestampMillis(endDate, startDate.Offset);

		// Assert — this FAILS because the startDate's offset (-5h) is applied
		// to endDate instead of endDate's own offset (-4h), shifting it by 1 hour.
		Assert.Equal(expectedEndUtcMillis, actual);
	}

	[Fact]
	public void ToFilterTimestampMillis_UtcDate_ShouldReturnSameValue()
	{
		// Arrange: a UTC date (offset = 0) should pass through unchanged
		var date = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
		var expectedMillis = date.ToUnixTimeMilliseconds();

		// Act
		var actual = CalendarStore.ToFilterTimestampMillis(date, date.Offset);

		// Assert — this passes even with the bug since offset is 0
		Assert.Equal(expectedMillis, actual);
	}
}
