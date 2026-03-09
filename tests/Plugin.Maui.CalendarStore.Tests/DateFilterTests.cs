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
		var actual = CalendarStore.ToFilterTimestampMillis(date);

		// Assert
		Assert.Equal(expectedUtcMillis, actual);
	}

	[Fact]
	public void ToFilterTimestampMillis_EndDate_ShouldUseOwnOffset_NotStartDateOffset()
	{
		// Arrange: start and end dates with different offsets (e.g. DST transition)
		var startDate = new DateTimeOffset(2025, 3, 8, 23, 0, 0, TimeSpan.FromHours(-5)); // EST
		var endDate = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.FromHours(-4));  // EDT

		var expectedEndUtcMillis = endDate.ToUnixTimeMilliseconds();

		// Act — previously the bug was that startDate.Offset was applied to endDate
		var actual = CalendarStore.ToFilterTimestampMillis(endDate);

		// Assert
		Assert.Equal(expectedEndUtcMillis, actual);
	}

	[Fact]
	public void ToFilterTimestampMillis_UtcDate_ShouldReturnSameValue()
	{
		// Arrange: a UTC date (offset = 0) should pass through unchanged
		var date = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
		var expectedMillis = date.ToUnixTimeMilliseconds();

		// Act
		var actual = CalendarStore.ToFilterTimestampMillis(date);

		// Assert
		Assert.Equal(expectedMillis, actual);
	}

	[Fact]
	public void BuildInstancesSelection_AlwaysExcludesDeleted()
	{
		var result = CalendarStore.BuildInstancesSelection("deleted", "calendar_id");

		Assert.Equal("deleted != 1", result);
	}

	[Fact]
	public void BuildInstancesSelection_WithCalendarId_AppendsFilter()
	{
		var result = CalendarStore.BuildInstancesSelection("deleted", "calendar_id", "42");

		Assert.Equal("deleted != 1 AND calendar_id = 42", result);
	}

	[Fact]
	public void BuildInstancesSelection_WithNullCalendarId_ExcludesDeletedOnly()
	{
		var result = CalendarStore.BuildInstancesSelection("deleted", "calendar_id", null);

		Assert.Equal("deleted != 1", result);
	}

	[Fact]
	public void BuildInstancesSelection_WithEmptyCalendarId_ExcludesDeletedOnly()
	{
		var result = CalendarStore.BuildInstancesSelection("deleted", "calendar_id", "");

		Assert.Equal("deleted != 1", result);
	}

	[Fact]
	public void GetOrAdd_ReturnsCachedValue_OnSecondCall()
	{
		var cache = new Dictionary<string, List<string>>();
		int factoryCalls = 0;

		var first = CalendarStore.GetOrAdd(cache, "key1", _ =>
		{
			factoryCalls++;
			return new List<string> { "a", "b" };
		});

		var second = CalendarStore.GetOrAdd(cache, "key1", _ =>
		{
			factoryCalls++;
			return new List<string> { "should not be called" };
		});

		Assert.Equal(1, factoryCalls);
		Assert.Same(first, second);
		Assert.Equal(new[] { "a", "b" }, first);
	}

	[Fact]
	public void GetOrAdd_CallsFactory_ForDifferentKeys()
	{
		var cache = new Dictionary<string, int>();

		var val1 = CalendarStore.GetOrAdd(cache, "a", _ => 1);
		var val2 = CalendarStore.GetOrAdd(cache, "b", _ => 2);

		Assert.Equal(1, val1);
		Assert.Equal(2, val2);
		Assert.Equal(2, cache.Count);
	}
}
