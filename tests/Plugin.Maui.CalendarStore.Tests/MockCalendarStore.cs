using Plugin.Maui.CalendarStore;

namespace Plugin.Maui.CalendarStore.Tests;

/// <summary>
/// Mock implementation of <see cref="ICalendarStore"/> for testing the
/// static <see cref="CalendarStore"/> facade.
/// </summary>
internal class MockCalendarStore : ICalendarStore
{
	public Func<Task<IEnumerable<Calendar>>>? OnGetCalendars { get; set; }
	public Func<string, Task<Calendar>>? OnGetCalendar { get; set; }
	public Func<string?, DateTimeOffset?, DateTimeOffset?, Task<IEnumerable<CalendarEvent>>>? OnGetEvents { get; set; }
	public Func<string, Task<CalendarEvent>>? OnGetEvent { get; set; }

	public Task<IEnumerable<Calendar>> GetCalendars() =>
		OnGetCalendars?.Invoke() ?? Task.FromResult<IEnumerable<Calendar>>(Array.Empty<Calendar>());

	public Task<Calendar> GetCalendar(string calendarId) =>
		OnGetCalendar?.Invoke(calendarId) ?? throw new NotImplementedException();

	public Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null,
		DateTimeOffset? startDate = null, DateTimeOffset? endDate = null) =>
		OnGetEvents?.Invoke(calendarId, startDate, endDate) ?? Task.FromResult<IEnumerable<CalendarEvent>>(Array.Empty<CalendarEvent>());

	public Task<CalendarEvent> GetEvent(string eventId) =>
		OnGetEvent?.Invoke(eventId) ?? throw new NotImplementedException();

	public Task<string> CreateCalendar(string name, Color? color = null) => Task.FromResult("mock-cal-id");
	public Task UpdateCalendar(string calendarId, string newName, Color? newColor = null) => Task.CompletedTask;
	public Task DeleteCalendar(string calendarId) => Task.CompletedTask;
	public Task DeleteCalendar(Calendar calendarToDelete) => Task.CompletedTask;
	public Task<string> CreateEvent(string calendarId, string title, string description, string location,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false, Reminder[]? reminders = null) => Task.FromResult("mock-event-id");
	public Task<string> CreateEvent(CalendarEvent calendarEvent) => Task.FromResult("mock-event-id");
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate) => Task.FromResult("mock-event-id");
	public Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, Reminder[]? reminders = null) => Task.CompletedTask;
	public Task UpdateEvent(CalendarEvent eventToUpdate) => Task.CompletedTask;
	public Task DeleteEvent(string eventId) => Task.CompletedTask;
	public Task DeleteEvent(CalendarEvent eventToDelete) => Task.CompletedTask;
}
