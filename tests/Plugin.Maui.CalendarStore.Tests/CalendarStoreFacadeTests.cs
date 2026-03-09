using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Tests the static <see cref="CalendarStore"/> facade to ensure it properly
/// delegates to the underlying <see cref="ICalendarStore"/> implementation
/// and always returns fresh data on each call.
/// </summary>
public sealed class CalendarStoreFacadeTests : IDisposable
{
	public CalendarStoreFacadeTests()
	{
		CalendarStore.SetDefault(null);
	}

	public void Dispose()
	{
		CalendarStore.SetDefault(null);
	}

	[Fact]
	public async Task GetEvents_DelegatesCallToImplementation()
	{
		var expectedEvent = CreateEvent("e1", "cal1", "Test Event");

		var mock = new MockCalendarStore
		{
			OnGetEvents = (_, _, _) =>
				Task.FromResult<IEnumerable<CalendarEvent>>(new[] { expectedEvent })
		};

		CalendarStore.SetDefault(mock);

		var events = (await CalendarStore.Default.GetEvents()).ToList();

		Assert.Single(events);
		Assert.Equal("e1", events[0].Id);
		Assert.Equal("Test Event", events[0].Title);
	}

	[Fact]
	public async Task GetEvents_ReturnsUpdatedData_OnSubsequentCalls()
	{
		// Simulates the scenario from issue #51: events change externally
		// and subsequent calls should reflect the new data.
		int callCount = 0;
		var mock = new MockCalendarStore
		{
			OnGetEvents = (_, _, _) =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult<IEnumerable<CalendarEvent>>(new[]
					{
						CreateEvent("e1", "cal1", "December Meeting",
							startMonth: 12, endMonth: 12)
					});
				}

				// Second call: event was moved to January externally
				return Task.FromResult<IEnumerable<CalendarEvent>>(new[]
				{
					CreateEvent("e1", "cal1", "December Meeting",
						startMonth: 1, endMonth: 1)
				});
			}
		};

		CalendarStore.SetDefault(mock);

		var firstResult = (await CalendarStore.Default.GetEvents()).ToList();
		Assert.Equal(12, firstResult[0].StartDate.Month);

		var secondResult = (await CalendarStore.Default.GetEvents()).ToList();
		Assert.Equal(1, secondResult[0].StartDate.Month);

		Assert.Equal(2, callCount);
	}

	[Fact]
	public async Task GetCalendars_DelegatesCallToImplementation()
	{
		var expectedCalendar = new Calendar("cal1", "Work", Colors.Blue, false);

		var mock = new MockCalendarStore
		{
			OnGetCalendars = () =>
				Task.FromResult<IEnumerable<Calendar>>(new[] { expectedCalendar })
		};

		CalendarStore.SetDefault(mock);

		var calendars = (await CalendarStore.Default.GetCalendars()).ToList();

		Assert.Single(calendars);
		Assert.Equal("cal1", calendars[0].Id);
		Assert.Equal("Work", calendars[0].Name);
	}

	[Fact]
	public async Task GetEvent_DelegatesCallToImplementation()
	{
		var expectedEvent = CreateEvent("e1", "cal1", "Meeting");

		var mock = new MockCalendarStore
		{
			OnGetEvent = (id) =>
			{
				Assert.Equal("e1", id);
				return Task.FromResult(expectedEvent);
			}
		};

		CalendarStore.SetDefault(mock);

		var result = await CalendarStore.Default.GetEvent("e1");

		Assert.Equal("Meeting", result.Title);
	}

	[Fact]
	public async Task GetEvents_PassesThroughFilterParameters()
	{
		string? capturedCalendarId = null;
		DateTimeOffset? capturedStart = null;
		DateTimeOffset? capturedEnd = null;

		var mock = new MockCalendarStore
		{
			OnGetEvents = (calId, start, end) =>
			{
				capturedCalendarId = calId;
				capturedStart = start;
				capturedEnd = end;
				return Task.FromResult<IEnumerable<CalendarEvent>>(Array.Empty<CalendarEvent>());
			}
		};

		CalendarStore.SetDefault(mock);

		var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var end = new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.Zero);

		await CalendarStore.Default.GetEvents("my-calendar", start, end);

		Assert.Equal("my-calendar", capturedCalendarId);
		Assert.Equal(start, capturedStart);
		Assert.Equal(end, capturedEnd);
	}

	/// <summary>
	/// Helper to create a <see cref="CalendarEvent"/> with internal-set properties.
	/// </summary>
	static CalendarEvent CreateEvent(string id, string calendarId, string title,
		int startMonth = 6, int endMonth = 6)
	{
		return new CalendarEvent(id, calendarId, title)
		{
			StartDate = new DateTimeOffset(2025, startMonth, 15, 10, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, endMonth, 15, 11, 0, 0, TimeSpan.Zero)
		};
	}
}
