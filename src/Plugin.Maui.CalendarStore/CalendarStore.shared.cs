namespace Plugin.Maui.CalendarStore;

/// <summary>
/// The CalendarStore API lets a user read information about the device's calendar and associated data.
/// </summary>
public static class CalendarStore
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

	/// <summary>
	/// Retrieves all available calendars from the device.
	/// </summary>
	/// <returns>A list of <see cref="Calendar"/> objects that each represent a calendar on the user's device.</returns>
	public static async Task<IEnumerable<Calendar>> GetCalendarsAsync() =>
        await Default.GetCalendars();

	/// <summary>
	/// Retrieves a specific calendar from the device.
	/// </summary>
	/// <param name="calendarId">The unique identifier of a calendar.</param>
	/// <returns>A <see cref="Calendar"/> object that represents the requested calendar from the user's device.</returns>
	/// <remarks>The ID format differentiates between platforms. For example, on Android it is an integer, on other platforms it can be a string or GUID.</remarks>
	public static async Task<Calendar> GetCalendarAsync(string calendarId) =>
        await Default.GetCalendar(calendarId);

	/// <summary>
	/// Retrieves events from a specific calendar or all calendars from the device.
	/// </summary>
	/// <param name="calendarId">The calendar identifier to retrieve events for. If not provided, events will be retrieved for all calendars on the device.</param>
	/// <param name="startDate">The start date of the range to retrieve events for.</param>
	/// <param name="endDate">The end date of the range to retrieve events for.</param>
	/// <returns>A list of events from the calendars on the device.</returns>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	public static async Task<IEnumerable<CalendarEvent>> GetEventsAsync(string? calendarId = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null) =>
        await Default.GetEvents(calendarId, startDate, endDate);

	/// <summary>
	/// Retrieves a specific event from the calendar store on the device.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to retrieve.</param>
	/// <returns>A <see cref="CalendarEvent"/> object that represents the requested event from the user's device.</returns>
	/// <exception cref="ArgumentException">Thrown when an event with the value specified in <paramref name="eventId"/> could not be found.</exception>
	public static async Task<CalendarEvent> GetEventAsync(string eventId) =>
        await Default.GetEvent(eventId);

    internal static ArgumentException InvalidCalendar(string calendarId) =>
        new($"No calendar exists with ID '{calendarId}'.", nameof(calendarId));

    internal static ArgumentException InvalidEvent(string eventId) =>
        new($"No event exists with ID '{eventId}'.", nameof(eventId));

	public class CalendarStoreException : Exception
	{
		public CalendarStoreException(string message)
			: base(message) { }
	}
}
