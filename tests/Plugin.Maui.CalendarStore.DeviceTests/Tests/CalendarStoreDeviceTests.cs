using Plugin.Maui.CalendarStore;
#if ANDROID
using Android.Content;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
#endif

namespace Plugin.Maui.CalendarStore.DeviceTests;

/// <summary>
/// Exercises the platform-specific <see cref="ICalendarStore"/> implementation on a
/// real device/simulator: calendar and recurring-event CRUD including single-occurrence
/// exceptions. Each test gets its own calendar so runs are isolated.
/// </summary>
[Collection("CalendarStore")]
public sealed class CalendarStoreDeviceTests : IAsyncLifetime
{
	const string timeZoneId = "America/New_York";
	const string title = "Recurring standup";

	static readonly TimeZoneInfo eventTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

	static readonly DateTimeOffset rangeStart = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
	static readonly DateTimeOffset rangeEnd = new(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);

	readonly ICalendarStore store = CalendarStore.Default;

	string calendarId = string.Empty;

	public async Task InitializeAsync()
	{
		calendarId = await store.CreateCalendar($"CalendarStore DeviceTests {Guid.NewGuid():N}");
	}

	public async Task DisposeAsync()
	{
		try
		{
			await store.DeleteCalendar(calendarId);
		}
		catch
		{
			// Best effort cleanup.
		}
	}

	[Fact]
	public async Task GetCalendars_ContainsTestCalendar()
	{
		var calendars = await store.GetCalendars();

		Assert.Contains(calendars, c => c.Id == calendarId);
	}

	[Fact]
	public async Task CreateEvent_RoundTripsBasicFields()
	{
		var start = InZone(2027, 2, 2, 14, 0);
		var end = start.AddHours(2);

		var eventId = await store.CreateEvent(calendarId, "One-off", "the description", "the location",
			start, end);

		var calendarEvent = await store.GetEvent(eventId);

		Assert.Equal("One-off", calendarEvent.Title);
		Assert.Equal("the description", calendarEvent.Description);
		Assert.Equal("the location", calendarEvent.Location);
		Assert.False(calendarEvent.IsAllDay);
		Assert.False(calendarEvent.IsRecurring);
		Assert.Null(calendarEvent.Recurrence);
		Assert.Equal(start, calendarEvent.StartDate);
		Assert.Equal(end, calendarEvent.EndDate);
	}

	[Fact]
	public async Task UpdateEvent_ChangesFields()
	{
		var start = InZone(2027, 2, 2, 14, 0);
		var eventId = await store.CreateEvent(calendarId, "Before update", "old", "old",
			start, start.AddHours(1));

		var newStart = InZone(2027, 2, 3, 11, 0);
		var newEnd = newStart.AddHours(1);

		await store.UpdateEvent(eventId, "After update", "new", "new", newStart, newEnd, false);

		var calendarEvent = await store.GetEvent(eventId);

		Assert.Equal("After update", calendarEvent.Title);
		Assert.Equal("new", calendarEvent.Description);
		Assert.Equal("new", calendarEvent.Location);
		Assert.Equal(newStart, calendarEvent.StartDate);
		Assert.Equal(newEnd, calendarEvent.EndDate);
	}

	[Fact]
	public async Task DeleteEvent_RemovesEvent()
	{
		var start = InZone(2027, 2, 2, 14, 0);
		var eventId = await store.CreateEvent(calendarId, "To be deleted", "", "",
			start, start.AddHours(1));

		await store.DeleteEvent(eventId);

		await Assert.ThrowsAsync<ArgumentException>(() => store.GetEvent(eventId));

		var remaining = await WaitForAsync(events => !events.Any(e => e.Id == eventId));
		Assert.DoesNotContain(remaining, e => e.Id == eventId);
	}

	[Fact]
	public async Task CreateAllDayEvent_MarksEventAsAllDay()
	{
		var day = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
		var eventId = await store.CreateAllDayEvent(calendarId, "All day one-off", "", "",
			day, day.AddDays(1));

		var calendarEvent = await store.GetEvent(eventId);

		Assert.True(calendarEvent.IsAllDay);
		Assert.False(calendarEvent.IsRecurring);
	}

	[Fact]
	public async Task CreateUpdateDeleteCalendar_RoundTrips()
	{
		var name = $"Temp calendar {Guid.NewGuid():N}";
		var createdId = await store.CreateCalendar(name);

		try
		{
			var created = await store.GetCalendar(createdId);
			Assert.Equal(name, created.Name);

			var newName = $"{name} renamed";
			await store.UpdateCalendar(createdId, newName);

			var updated = await store.GetCalendar(createdId);
			Assert.Equal(newName, updated.Name);
		}
		finally
		{
			await store.DeleteCalendar(createdId);
		}

		var calendars = await store.GetCalendars();
		Assert.DoesNotContain(calendars, c => c.Id == createdId);
	}

	[Fact]
	public async Task CreateRecurringEvent_ExpandsIntoOccurrences()
	{
		await CreateSeriesAsync();

		var occurrences = await WaitForCountAsync(3);

		var localDates = occurrences
			.Select(e => Local(e.StartDate).DateTime)
			.OrderBy(d => d)
			.ToArray();

		Assert.Equal(
			[
				new DateTime(2027, 1, 4, 9, 0, 0),
				new DateTime(2027, 1, 6, 9, 0, 0),
				new DateTime(2027, 1, 11, 9, 0, 0),
			],
			localDates);
	}

	[Fact]
	public async Task RecurringOccurrences_ExposeRecurrenceAndTimeZone()
	{
		await CreateSeriesAsync();

		var occurrences = await WaitForCountAsync(3);

		foreach (var occurrence in occurrences)
		{
			Assert.True(occurrence.IsRecurring);
			Assert.NotNull(occurrence.Recurrence);
			Assert.Equal(RecurrenceFrequency.Weekly, occurrence.Recurrence!.Frequency);
			Assert.Equal(1, occurrence.Recurrence.Interval);
			Assert.Equal(timeZoneId, occurrence.TimeZoneId);
			Assert.NotNull(occurrence.OriginalOccurrenceStart);
			Assert.False(occurrence.IsDetached);
		}
	}

	[Fact]
	public async Task UpdateSingleOccurrence_OnlyChangesThatOccurrence()
	{
		await CreateSeriesAsync();

		var occurrences = await WaitForCountAsync(3);
		var middle = occurrences.Single(e => Local(e.StartDate).Date == new DateTime(2027, 1, 6));

		var movedStart = InZone(2027, 1, 6, 15, 0);
		await store.UpdateEvent(middle.Id, "Moved standup", "desc", "loc",
			movedStart, movedStart.AddHours(1), false, null,
			RecurrenceScope.ThisEvent, middle.OriginalOccurrenceStart);

		var updated = await WaitForAsync(events => events.Any(e => e.Title == "Moved standup"));

		var moved = updated.Single(e => e.Title == "Moved standup");
		Assert.Equal(new DateTime(2027, 1, 6, 15, 0, 0), Local(moved.StartDate).DateTime);
		Assert.Equal(new DateTime(2027, 1, 6, 0, 0, 0), moved.OriginalOccurrenceStart?.Date);
		Assert.True(moved.IsDetached);

		var untouched = updated.Where(e => e.Title == title).ToList();
		Assert.Equal(2, untouched.Count);
		Assert.All(untouched, e => Assert.Equal(9, Local(e.StartDate).Hour));
	}

	[Fact]
	public async Task MovedOccurrence_CanBeUpdatedAgainAndDeleted()
	{
		await CreateSeriesAsync();

		var occurrences = await WaitForCountAsync(3);
		var middle = occurrences.Single(e => Local(e.StartDate).Date == new DateTime(2027, 1, 6));
		var firstMove = InZone(2027, 1, 8, 15, 0);

		await store.UpdateEvent(middle.Id, "First move", "", "", firstMove,
			firstMove.AddHours(1), false, null, RecurrenceScope.ThisEvent,
			middle.OriginalOccurrenceStart);

		var afterFirstMove = await WaitForAsync(events => events.Any(e => e.Title == "First move"));
		var moved = afterFirstMove.Single(e => e.Title == "First move");
		var secondMove = InZone(2027, 1, 9, 16, 0);

		await store.UpdateEvent(moved.Id, "Second move", "", "", secondMove,
			secondMove.AddHours(1), false, null, RecurrenceScope.ThisEvent,
			moved.OriginalOccurrenceStart);

		var afterSecondMove = await WaitForAsync(events =>
			events.Count == 3 && events.Count(e => e.Title == "Second move") == 1);
		Assert.DoesNotContain(afterSecondMove, e => e.Title == "First move");

		var movedAgain = afterSecondMove.Single(e => e.Title == "Second move");
		await store.DeleteEvent(movedAgain.Id, RecurrenceScope.ThisEvent,
			movedAgain.OriginalOccurrenceStart);

		var remaining = await WaitForCountAsync(2);
		Assert.DoesNotContain(remaining, e => e.Title == "Second move");
	}

	[Fact]
	public async Task DeleteSeries_RemovesDetachedOccurrences()
	{
		var seriesId = await CreateSeriesAsync();
		var occurrences = await WaitForCountAsync(3);
		var middle = occurrences.Single(e => Local(e.StartDate).Date == new DateTime(2027, 1, 6));
		var movedStart = InZone(2027, 1, 8, 15, 0);

		await store.UpdateEvent(middle.Id, "Detached move", "", "", movedStart,
			movedStart.AddHours(1), false, null, RecurrenceScope.ThisEvent,
			middle.OriginalOccurrenceStart);
		await WaitForAsync(events => events.Any(e => e.Title == "Detached move"));

		await store.DeleteEvent(seriesId, RecurrenceScope.AllEvents);

		Assert.Empty(await WaitForCountAsync(0));
	}

	[Fact]
	public async Task RecurringReminders_AreRelativeToEachAndroidOccurrence()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return;
		}

		var anchor = InZone(2027, 1, 4, 9, 0);
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Daily,
			Count = 3,
		};

		await store.CreateEvent(calendarId, "Series with reminder", "", "", anchor,
			anchor.AddHours(1), false, [new Reminder(anchor.AddMinutes(-15))],
			recurrence, timeZoneId);

		var occurrences = await WaitForCountAsync(3);
		foreach (var occurrence in occurrences)
		{
			var reminder = Assert.Single(occurrence.Reminders);
			Assert.Equal(TimeSpan.FromMinutes(15), occurrence.StartDate - reminder.DateTime);
		}
	}

	[Fact]
	public async Task GetEvents_PreservesAndroidEventColor()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return;
		}

#if ANDROID
		var start = InZone(2027, 1, 20, 10, 0);
		var eventId = await store.CreateEvent(calendarId, "Colored event", "", "",
			start, start.AddHours(1));
		var values = new ContentValues();
		values.Put(CalendarContract.Events.InterfaceConsts.EventColor,
			unchecked((int)0xff123456));
		var eventUri = ContentUris.WithAppendedId(CalendarContract.Events.ContentUri!,
			long.Parse(eventId));
		var updated = Platform.AppContext.ApplicationContext!.ContentResolver!
			.Update(eventUri, values, null, null);
		Assert.Equal(1, updated);

		var events = await WaitForAsync(items => items.Any(e => e.Id == eventId));
		Assert.NotNull(events.Single(e => e.Id == eventId).EventColor);
#endif
	}

	[Fact]
	public async Task WindowsWeeklyRecurrence_DefaultsToAnchorWeekday()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		var anchor = InZone(2027, 1, 4, 9, 0);
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Weekly,
			Count = 2,
		};

		await store.CreateEvent(calendarId, "Default weekly", "", "", anchor,
			anchor.AddHours(1), false, null, recurrence, timeZoneId);

		var occurrences = await WaitForCountAsync(2);
		Assert.All(occurrences, occurrence =>
			Assert.Equal(DayOfWeek.Monday, Local(occurrence.StartDate).DayOfWeek));
	}

	[Fact]
	public async Task WindowsNegativeMonthDay_IsRejected()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		var anchor = InZone(2027, 1, 4, 9, 0);
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Monthly,
			DaysOfMonth = { -1 },
		};

		await Assert.ThrowsAsync<NotSupportedException>(() => store.CreateEvent(
			calendarId, "Last day", "", "", anchor, anchor.AddHours(1), false,
			null, recurrence, timeZoneId));
	}

	[Fact]
	public async Task WindowsOneOffEvent_UsesAbsoluteDurationAcrossDstChange()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		var start = new DateTimeOffset(2027, 3, 14, 1, 30, 0, TimeSpan.FromHours(-5));
		var end = new DateTimeOffset(2027, 3, 14, 3, 30, 0, TimeSpan.FromHours(-4));
		var eventId = await store.CreateEvent(calendarId, "DST event", "", "", start, end);

		var calendarEvent = await store.GetEvent(eventId);
		Assert.Equal(TimeSpan.FromHours(1), calendarEvent.Duration);
	}

	[Fact]
	public async Task DeleteSingleOccurrence_LeavesOtherOccurrences()
	{
		await CreateSeriesAsync();

		var occurrences = await WaitForCountAsync(3);
		var middle = occurrences.Single(e => Local(e.StartDate).Date == new DateTime(2027, 1, 6));

		await store.DeleteEvent(middle.Id, RecurrenceScope.ThisEvent, middle.OriginalOccurrenceStart);

		var remaining = await WaitForCountAsync(2);

		var localDates = remaining
			.Select(e => Local(e.StartDate).DateTime)
			.OrderBy(d => d)
			.ToArray();

		var expected = new[]
		{
			new DateTime(2027, 1, 4, 9, 0, 0),
			new DateTime(2027, 1, 11, 9, 0, 0),
		};

		Assert.Equal(expected, localDates);
	}

	[Fact]
	public async Task DeleteEvent_RemovesWholeSeries()
	{
		var eventId = await CreateSeriesAsync();

		await store.DeleteEvent(eventId, RecurrenceScope.AllEvents);

		var remaining = await WaitForCountAsync(0);
		Assert.Empty(remaining);
	}

	[Fact]
	public async Task CreateAllDayRecurringEvent_ExpandsAsAllDayOccurrences()
	{
		var anchor = new DateTimeOffset(2027, 1, 4, 0, 0, 0, TimeSpan.Zero);
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Daily,
			Interval = 1,
			Count = 3,
		};

		await store.CreateEvent(calendarId, "All day series", "desc", "loc",
			anchor, anchor.AddDays(1), true, null, recurrence, timeZoneId);

		var occurrences = await WaitForCountAsync(3);
		Assert.All(occurrences, e => Assert.True(e.IsAllDay));
	}

	async Task<string> CreateSeriesAsync()
	{
		var anchor = InZone(2027, 1, 4, 9, 0);
		var recurrence = new CalendarRecurrence
		{
			Frequency = RecurrenceFrequency.Weekly,
			Interval = 1,
			DaysOfWeek = { new(DayOfWeek.Monday), new(DayOfWeek.Wednesday) },
			Count = 3,
		};

		return await store.CreateEvent(calendarId, title, "desc", "loc",
			anchor, anchor.AddHours(1), false, null, recurrence, timeZoneId);
	}

	async Task<List<CalendarEvent>> WaitForCountAsync(int expectedCount) =>
		await WaitForAsync(events => events.Count == expectedCount);

	async Task<List<CalendarEvent>> WaitForAsync(Func<List<CalendarEvent>, bool> predicate)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

		while (true)
		{
			var events = (await store.GetEvents(calendarId, rangeStart, rangeEnd)).ToList();

			if (predicate(events) || DateTimeOffset.UtcNow >= deadline)
			{
				return events;
			}

			await Task.Delay(250);
		}
	}

	static DateTimeOffset InZone(int year, int month, int day, int hour, int minute)
	{
		var wall = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
		return new DateTimeOffset(wall, eventTimeZone.GetUtcOffset(wall));
	}

	static DateTimeOffset Local(DateTimeOffset value) =>
		TimeZoneInfo.ConvertTime(value, eventTimeZone);
}
