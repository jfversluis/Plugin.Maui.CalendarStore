namespace Plugin.Maui.CalendarStore;

/// <summary>
/// The CalendarStore API lets a user read information about the device's calendar and associated data.
/// </summary>
public interface ICalendarStore
{
	/// <summary>
	/// Retrieves all available calendars from the device.
	/// </summary>
	/// <returns>A list of <see cref="Calendar"/> objects that each represent a calendar on the user's device.</returns>
	Task<IEnumerable<Calendar>> GetCalendars();

	/// <summary>
	/// Retrieves a specific calendar from the device.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to retrieve.</param>
	/// <returns>A <see cref="Calendar"/> object that represents the requested calendar from the user's device.</returns>
	/// <remarks>The ID format differentiates between platforms. For example, on Android it is an integer, on other platforms it can be a string or GUID.</remarks>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	Task<Calendar> GetCalendar(string calendarId);

	/// <summary>
	/// Retrieves events from a specific calendar or all calendars from the device.
	/// </summary>
	/// <param name="calendarId">The calendar identifier to retrieve events for. If not provided, events will be retrieved for all calendars on the device.</param>
	/// <param name="startDate">The start date of the range to retrieve events for.</param>
	/// <param name="endDate">The end date of the range to retrieve events for.</param>
	/// <returns>A list of events from the calendars on the device.</returns>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null);

	/// <summary>
	/// Retrieves a specific event from the calendar store on the device.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to retrieve.</param>
	/// <returns>A <see cref="CalendarEvent"/> object that represents the requested event from the user's device.</returns>
	/// <exception cref="ArgumentException">Thrown when an event with the value specified in <paramref name="eventId"/> could not be found.</exception>
	Task<CalendarEvent> GetEvent(string eventId);

	/// <summary>
	/// Creates a new event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="startDateTime">The start date and time for the event.</param>
	/// <param name="endDateTime">The end date and time for the event.</param>
	/// <returns></returns>
	Task CreateEvent(string calendarId, string title, string description, DateTimeOffset startDateTime, DateTimeOffset endDateTime);

	/// <summary>
	/// Creates a new event based on the provided <paramref name="calendarEvent"/> object. 
	/// </summary>
	/// <param name="calendarEvent">The event object with the details to save to the calendar specified in this object.</param>
	/// <returns></returns>
	Task CreateEvent(CalendarEvent calendarEvent);

	/// <summary>
	/// Creates a new all day event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to save the event in.</param>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="startDate">The start date for the event.</param>
	/// <param name="endDate">The end date for the event.</param>
	/// <returns></returns>
	/// <remarks>Any time information in <paramref name="startDate"/> and <paramref name="endDate"/> is omitted.</remarks>
	Task CreateAllDayEvent(string calendarId, string title, string description, DateTimeOffset startDate, DateTimeOffset endDate);
}