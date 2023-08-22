namespace Plugin.Maui.Calendar;

partial class FeatureImplementation : ICalendars
{
	public Task<Calendar> GetCalendarAsync(string calendarId)
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<Calendar>> GetCalendarsAsync()
	{
		throw new NotImplementedException();
	}

	public Task<CalendarEvent> GetEventAsync(string eventId)
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<CalendarEvent>> GetEventsAsync(string calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		throw new NotImplementedException();
	}
}