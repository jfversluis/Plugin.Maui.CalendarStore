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

	/// <summary>
	/// Creates a new event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="location">The location of the event.</param>
	/// <param name="startDateTime">The start date and time for the event.</param>
	/// <param name="endDateTime">The end date and time for the event.</param>
	/// <param name="isAllDay">Indicates whether or not this event should be marked as an all-day event.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <remarks>When <paramref name="isAllDay"/> is set to <see langword="true"/>, any time information in <paramref name="startDateTime"/> and <paramref name="endDateTime"/> is omitted.</remarks>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	public static async Task CreateEvent(string calendarId, string title, string description, string location,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false) =>
		await Default.CreateEvent(calendarId, title, description, location, startDateTime,
			endDateTime, isAllDay);

	/// <summary>
	/// Creates a new event based on the provided <paramref name="calendarEvent"/> object. 
	/// </summary>
	/// <param name="calendarEvent">The event object with the details to save to the calendar specified in this object.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	public static async Task CreateEvent(CalendarEvent calendarEvent) =>
		await Default.CreateEvent(calendarEvent);

	/// <summary>
	/// Creates a new all day event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to save the event in.</param>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="location">The location of the event.</param>
	/// <param name="startDate">The start date for the event.</param>
	/// <param name="endDate">The end date for the event.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <remarks>Any time information in <paramref name="startDate"/> and <paramref name="endDate"/> is omitted.</remarks>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	public static async Task CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate) =>
		await Default.CreateAllDayEvent(calendarId, title, description, location,
			startDate, endDate);

	internal static ArgumentException InvalidCalendar(string calendarId) =>
        new($"No calendar exists with ID '{calendarId}'.", nameof(calendarId));

    internal static ArgumentException InvalidEvent(string eventId) =>
        new($"No event exists with ID '{eventId}'.", nameof(eventId));
}
