namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	public Task CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		throw new NotImplementedException();
	}

	public Task CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay = false)
	{
		throw new NotImplementedException();
	}

	public Task CreateEvent(CalendarEvent calendarEvent)
	{
		throw new NotImplementedException();
	}

	public Task<Calendar> GetCalendar(string calendarId)
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<Calendar>> GetCalendars()
	{
		throw new NotImplementedException();
	}

	public Task<CalendarEvent> GetEvent(string eventId)
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null,
		DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		throw new NotImplementedException();
	}

	public Task RemoveEvent(string eventId)
	{
		throw new NotImplementedException();
	}

	public Task RemoveEvent(CalendarEvent @event)
	{
		throw new NotImplementedException();
	}
}