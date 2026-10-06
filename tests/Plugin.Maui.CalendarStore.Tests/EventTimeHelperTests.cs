using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Tests for the wall-clock and event time zone helpers used when writing events.
/// </summary>
public class EventTimeHelperTests
{
	static readonly TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone(
		"Tests/PlusTwo", TimeSpan.FromHours(2), "Tests +2", "Tests +2");

	// Standard time UTC-5, daylight time UTC-4, spring forward 2nd Sunday of March
	// at 02:00 and fall back 1st Sunday of November at 02:00.
	static readonly TimeZoneInfo dstZone = TimeZoneInfo.CreateCustomTimeZone(
		"Tests/Dst", TimeSpan.FromHours(-5), "Tests DST", "Tests Standard",
		"Tests Daylight",
		[
			TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
				DateTime.MinValue.Date, DateTime.MaxValue.Date,
				TimeSpan.FromHours(1),
				TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
					new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
				TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
					new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
		]);

	[Fact]
	public void ToWallTimeInstant_UsesOffsetOfProvidedZone()
	{
		var result = CalendarStore.ToWallTimeInstant(new DateTime(2025, 6, 15, 10, 0, 0), plusTwo);

		Assert.Equal(TimeSpan.FromHours(2), result.Offset);
		Assert.Equal(new DateTime(2025, 6, 15, 10, 0, 0), result.DateTime);
		Assert.Equal(new DateTime(2025, 6, 15, 8, 0, 0, DateTimeKind.Utc), result.UtcDateTime);
	}

	[Fact]
	public void ToWallTimeInstant_ResolvesStandardAndDaylightOffsets()
	{
		var winter = CalendarStore.ToWallTimeInstant(new DateTime(2025, 1, 15, 10, 0, 0), dstZone);
		var summer = CalendarStore.ToWallTimeInstant(new DateTime(2025, 7, 15, 10, 0, 0), dstZone);

		Assert.Equal(TimeSpan.FromHours(-5), winter.Offset);
		Assert.Equal(TimeSpan.FromHours(-4), summer.Offset);
	}

	[Fact]
	public void ToWallTimeInstant_MovesForwardOutOfDaylightSavingGap()
	{
		// 2025-03-09 02:30 does not exist in this zone (clock jumps 02:00 -> 03:00).
		var result = CalendarStore.ToWallTimeInstant(new DateTime(2025, 3, 9, 2, 30, 0), dstZone);

		Assert.False(dstZone.IsInvalidTime(result.DateTime));
		Assert.Equal(new DateTime(2025, 3, 9, 3, 0, 0), result.DateTime);
		Assert.Equal(TimeSpan.FromHours(-4), result.Offset);
	}

	[Fact]
	public void ResolveEventTimeZone_NullOrEmpty_ReturnsNull()
	{
		Assert.Null(CalendarStore.ResolveEventTimeZone(null));
		Assert.Null(CalendarStore.ResolveEventTimeZone(string.Empty));
		Assert.Null(CalendarStore.ResolveEventTimeZone("   "));
	}

	[Fact]
	public void ResolveEventTimeZone_Known_ReturnsZone()
	{
		var resolved = CalendarStore.ResolveEventTimeZone(plusTwo.Id);

		Assert.NotNull(resolved);
		Assert.Equal(TimeSpan.FromHours(2), resolved!.BaseUtcOffset);
	}

	[Fact]
	public void ResolveEventTimeZone_Unknown_FallsBackToLocal()
	{
		Assert.Equal(TimeZoneInfo.Local, CalendarStore.ResolveEventTimeZone("Not/ARealZone"));
	}

	[Fact]
	public void ResolveStoredTimes_NullTimeZone_ReturnsProvidedInstants()
	{
		var start = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.FromHours(-5));
		var end = start.AddHours(1);

		var (resolvedStart, resolvedEnd) = CalendarStore.ResolveStoredTimes(start, end, false, null);

		Assert.Equal(start, resolvedStart);
		Assert.Equal(end, resolvedEnd);
	}

	[Fact]
	public void ResolveStoredTimes_WithTimeZone_ReinterpretsWallClock()
	{
		// The provided offset is deliberately wrong for the target zone; the
		// wall-clock component must be re-anchored in +2.
		var start = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.FromHours(-5));
		var end = start.AddHours(1);

		var (resolvedStart, resolvedEnd) = CalendarStore.ResolveStoredTimes(start, end, false, plusTwo);

		Assert.Equal(TimeSpan.FromHours(2), resolvedStart.Offset);
		Assert.Equal(new DateTime(2025, 6, 15, 10, 0, 0), resolvedStart.DateTime);
		Assert.Equal(new DateTime(2025, 6, 15, 11, 0, 0), resolvedEnd.DateTime);
	}

	[Fact]
	public void ResolveStoredTimes_AllDay_NormalizesToUtcMidnight()
	{
		var start = new DateTimeOffset(2025, 6, 15, 23, 30, 0, TimeSpan.FromHours(-5));
		var end = new DateTimeOffset(2025, 6, 16, 23, 30, 0, TimeSpan.FromHours(-5));

		var (resolvedStart, resolvedEnd) = CalendarStore.ResolveStoredTimes(start, end, true, plusTwo);

		Assert.Equal(TimeSpan.Zero, resolvedStart.Offset);
		Assert.Equal(new DateTime(2025, 6, 15, 0, 0, 0), resolvedStart.DateTime);
		Assert.Equal(new DateTime(2025, 6, 16, 0, 0, 0), resolvedEnd.DateTime);
	}

	[Fact]
	public void ComputeDuration_CanUseAbsoluteTimeAcrossOffsetTransition()
	{
		var start = new DateTimeOffset(2027, 3, 14, 1, 30, 0, TimeSpan.FromHours(-5));
		var end = new DateTimeOffset(2027, 3, 14, 3, 30, 0, TimeSpan.FromHours(-4));

		var wallClockDuration = CalendarStore.ComputeDuration(start, end, false);
		var absoluteDuration = CalendarStore.ComputeDuration(start, end, false,
			useWallClockTime: false);

		Assert.Equal(TimeSpan.FromHours(2), wallClockDuration);
		Assert.Equal(TimeSpan.FromHours(1), absoluteDuration);
	}
}
