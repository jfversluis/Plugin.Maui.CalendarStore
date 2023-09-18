using Microsoft.Maui.Graphics;

namespace Plugin.Maui.CalendarStore;

/// <summary>
/// The CalendarStore API lets a user read information about the device's calendar and associated data.
/// </summary>
public static partial class CalendarStore
{
	static ICalendarStore? defaultImplementation;

	/// <summary>
	/// Provides the default implementation for static usage of this API.
	/// </summary>
	public static ICalendarStore Default =>
		defaultImplementation ??= new CalendarStoreImplementation();

	internal static void SetDefault(ICalendarStore? implementation) =>
		defaultImplementation = implementation;

	internal static readonly TimeSpan defaultStartTimeFromNow = TimeSpan.Zero;
	internal static readonly TimeSpan defaultEndTimeFromStartTime = TimeSpan.FromDays(14);

	/// <inheritdoc cref="ICalendarStore.GetCalendars"/>
	public static async Task<IEnumerable<Calendar>> GetCalendarsAsync() =>
        await Default.GetCalendars();

	/// <inheritdoc cref="ICalendarStore.GetCalendar(string)"/>
	public static async Task<Calendar> GetCalendarAsync(string calendarId) =>
        await Default.GetCalendar(calendarId);

	/// <inheritdoc cref="ICalendarStore.CreateCalendar(string, Color?)"/>
	public static async Task CreateCalendar(string name, Color? color = null) =>
		await Default.CreateCalendar(name, color);

	/// <inheritdoc cref="ICalendarStore.DeleteCalendar(string)"/>
	public static async Task DeleteCalendar(string calendarId) =>
		await Default.DeleteCalendar(calendarId);

	/// <inheritdoc cref="ICalendarStore.GetEvents(string?, DateTimeOffset?, DateTimeOffset?)"/>
	public static async Task<IEnumerable<CalendarEvent>> GetEventsAsync(string? calendarId = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null) =>
        await Default.GetEvents(calendarId, startDate, endDate);

	/// <inheritdoc cref="ICalendarStore.GetEvent(string)"/>
	public static async Task<CalendarEvent> GetEventAsync(string eventId) =>
        await Default.GetEvent(eventId);

	/// <inheritdoc cref="ICalendarStore.CreateEvent(string, string, string, string, DateTimeOffset, DateTimeOffset, bool)"/>
	public static async Task CreateEvent(string calendarId, string title, string description, string location,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false) =>
		await Default.CreateEvent(calendarId, title, description, location, startDateTime,
			endDateTime, isAllDay);

	/// <inheritdoc cref="ICalendarStore.CreateEvent(CalendarEvent)"/>
	public static async Task CreateEvent(CalendarEvent calendarEvent) =>
		await Default.CreateEvent(calendarEvent);

	/// <inheritdoc cref="ICalendarStore.CreateAllDayEvent(string, string, string, string, DateTimeOffset, DateTimeOffset)"/>
	public static async Task CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate) =>
		await Default.CreateAllDayEvent(calendarId, title, description, location,
			startDate, endDate);

	/// <inheritdoc cref="ICalendarStore.DeleteEvent(string)"/>
	public static async Task DeleteEvent(string eventId) =>
		await Default.DeleteEvent(eventId);

	/// <inheritdoc cref="ICalendarStore.DeleteEvent(CalendarEvent)"/>
	public static Task RemoveEvent(CalendarEvent @event) =>
		DeleteEvent(@event.Id);

	internal static ArgumentException InvalidCalendar(string calendarId) =>
        new($"No calendar exists with ID '{calendarId}'.", nameof(calendarId));

    internal static ArgumentException InvalidEvent(string eventId) =>
        new($"No event exists with ID '{eventId}'.", nameof(eventId));
}
