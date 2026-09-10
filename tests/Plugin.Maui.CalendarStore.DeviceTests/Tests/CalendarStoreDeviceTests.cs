using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.DeviceTests;

/// <summary>
/// Exercises the platform-specific <see cref="ICalendarStore"/> implementation on a
/// real device/simulator: calendar and recurring-event CRUD including single-occurrence
/// exceptions.
/// </summary>
[Collection("CalendarStore")]
public sealed class CalendarStoreDeviceTests : IClassFixture<CalendarFixture>
{
	const string TimeZoneId = "America/New_York";
	const string Title = "Recurring standup";

	static readonly TimeZoneInfo eventTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

	static readonly DateTimeOffset rangeStart = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
	static readonly DateTimeOffset rangeEnd = new(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);

	readonly ICalendarStore store;
	readonly string calendarId;

	public CalendarStoreDeviceTests(CalendarFixture fixture)
	{
		store = fixture.Store;
		calendarId = fixture.CalendarId;
	}

	[Fact]
	public async Task GetCalendars_ContainsFixtureCalendar()
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

		try
		{
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
		finally
		{
			await TryDeleteAsync(eventId);
		}
	}

	[Fact]
	public async Task UpdateEvent_ChangesFields()
	{
		var start = InZone(2027, 2, 2, 14, 0);
		var eventId = await store.CreateEvent(calendarId, "Before update", "old", "old",
			start, start.AddHours(1));

		try
		{
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
		finally
		{
			await TryDeleteAsync(eventId);
		}
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

		try
		{
			var calendarEvent = await store.GetEvent(eventId);

			Assert.True(calendarEvent.IsAllDay);
			Assert.False(calendarEvent.IsRecurring);
		}
		finally
		{
			await TryDeleteAsync(eventId);
		}
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
		var eventId = await CreateSeriesAsync();

		try
		{
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
		finally
		{
			await TryDeleteAsync(eventId);
		}
	}

	[Fact]
	public async Task RecurringOccurrences_ExposeRecurrenceAndTimeZone()
	{
		var eventId = await CreateSeriesAsync();

		try
		{
			var occurrences = await WaitForCountAsync(3);

			foreach (var occurrence in occurrences)
			{
				Assert.True(occurrence.IsRecurring);
				Assert.NotNull(occurrence.Recurrence);
				Assert.Equal(RecurrenceFrequency.Weekly, occurrence.Recurrence!.Frequency);
				Assert.Equal(1, occurrence.Recurrence.Interval);
				Assert.Equal(TimeZoneId, occurrence.TimeZoneId);
				Assert.NotNull(occurrence.OriginalOccurrenceStart);
				Assert.False(occurrence.IsDetached);
			}
		}
		finally
		{
			await TryDeleteAsync(eventId);
		}
	}

	[Fact]
	public async Task UpdateSingleOccurrence_OnlyChangesThatOccurrence()
	{
		var eventId = await CreateSeriesAsync();

		try
		{
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

			var untouched = updated.Where(e => e.Title == Title).ToList();
			Assert.Equal(2, untouched.Count);
			Assert.All(untouched, e => Assert.Equal(9, Local(e.StartDate).Hour));
		}
		finally
		{
			await TryDeleteAsync(eventId);
		}
	}

	[Fact]
	public async Task DeleteSingleOccurrence_LeavesOtherOccurrences()
	{
		var eventId = await CreateSeriesAsync();

		try
		{
			var occurrences = await WaitForCountAsync(3);
			var middle = occurrences.Single(e => Local(e.StartDate).Date == new DateTime(2027, 1, 6));

			await store.DeleteEvent(middle.Id, RecurrenceScope.ThisEvent, middle.OriginalOccurrenceStart);

			var remaining = await WaitForCountAsync(2);

			var localDates = remaining
				.Select(e => Local(e.StartDate).DateTime)
				.OrderBy(d => d)
				.ToArray();

			Assert.Equal(
				[
					new DateTime(2027, 1, 4, 9, 0, 0),
					new DateTime(2027, 1, 11, 9, 0, 0),
				],
				localDates);
		}
		finally
		{
			await TryDeleteAsync(eventId);
		}
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

		var eventId = await store.CreateEvent(calendarId, "All day series", "desc", "loc",
			anchor, anchor.AddDays(1), true, null, recurrence, TimeZoneId);

		try
		{
			var occurrences = await WaitForCountAsync(3);
			Assert.All(occurrences, e => Assert.True(e.IsAllDay));
		}
		finally
		{
			await TryDeleteAsync(eventId);
		}
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

		return await store.CreateEvent(calendarId, Title, "desc", "loc",
			anchor, anchor.AddHours(1), false, null, recurrence, TimeZoneId);
	}

	async Task<List<CalendarEvent>> WaitForCountAsync(int expectedCount)
	{
		return await WaitForAsync(events => events.Count == expectedCount);
	}

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

	async Task TryDeleteAsync(string eventId)
	{
		try
		{
			await store.DeleteEvent(eventId, RecurrenceScope.AllEvents);
		}
		catch
		{
			// Best effort cleanup.
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
