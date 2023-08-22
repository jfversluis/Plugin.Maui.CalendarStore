namespace Plugin.Maui.Calendar;

/// <summary>
/// TODO: Provide relevant comments for your APIs
/// </summary>
public interface ICalendars
{
	Task<IEnumerable<Calendar>> GetCalendarsAsync();

	Task<Calendar> GetCalendarAsync(string calendarId);

	Task<IEnumerable<CalendarEvent>> GetEventsAsync(string calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null);

	Task<CalendarEvent> GetEventAsync(string eventId);
}