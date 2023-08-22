namespace Plugin.Maui.Calendar;

public static class Calendars
{
	static ICalendars? defaultImplementation;

	/// <summary>
	/// Provides the default implementation for static usage of this API.
	/// </summary>
	public static ICalendars Default =>
		defaultImplementation ??= new FeatureImplementation();

	internal static void SetDefault(ICalendars? implementation) =>
		defaultImplementation = implementation;

	internal static readonly TimeSpan defaultStartTimeFromNow = TimeSpan.Zero;
	internal static readonly TimeSpan defaultEndTimeFromStartTime = TimeSpan.FromDays(14);

    public static async Task<IEnumerable<Calendar>> GetCalendarsAsync() =>
        await Default.GetCalendarsAsync();

    public static async Task<Calendar> GetCalendarAsync(string calendarId) =>
        await Default.GetCalendarAsync(calendarId);

    public static async Task<IEnumerable<CalendarEvent>> GetEventsAsync(string calendarId = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null) =>
        await Default.GetEventsAsync(calendarId, startDate, endDate);

    public static async Task<CalendarEvent> GetEventAsync(string eventId) =>
        await Default.GetEventAsync(eventId);

    internal static ArgumentException InvalidCalendar(string calendarId) =>
        new($"No calendar exists with ID '{calendarId}'.", nameof(calendarId));

    internal static ArgumentException InvalidEvent(string eventId) =>
        new($"No event exists with ID '{eventId}'.", nameof(eventId));
}
