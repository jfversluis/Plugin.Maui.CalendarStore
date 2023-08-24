namespace Plugin.Maui.CalendarStore;

partial class FeatureImplementation : ICalendars
{
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
}