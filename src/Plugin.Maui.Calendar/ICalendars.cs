namespace Plugin.Maui.Calendar;

/// <summary>
/// The Calendars API lets a user read information about the device's calendar and associated data.
/// </summary>
public interface ICalendars
{
	/// <summary>
	/// Retrieves all available calendars from the device.
	/// </summary>
	/// <returns>A list of <see cref="Calendar"/> objects that each represent a calendar on the user's device.</returns>
	Task<IEnumerable<Calendar>> GetCalendarsAsync();

	/// <summary>
	/// Retrieves a specific calendar from the device.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to retrieve.</param>
	/// <returns>A <see cref="Calendar"/> object that represents the requested calendar from the user's device.</returns>
	/// <remarks>The ID format differentiates between platforms. For example, on Android it is an integer, on other platforms it can be a string or GUID.</remarks>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	Task<Calendar> GetCalendarAsync(string calendarId);

	/// <summary>
	/// Retrieves events from a specific calendar or all calendars from the device.
	/// </summary>
	/// <param name="calendarId">The calendar identifier to retrieve events for. If not provided, events will be retrieved for all calendars on the device.</param>
	/// <param name="startDate">The start date of the range to retrieve events for.</param>
	/// <param name="endDate">The end date of the range to retrieve events for.</param>
	/// <returns>A list of events from the calendars on the device.</returns>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	Task<IEnumerable<CalendarEvent>> GetEventsAsync(string? calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null);

	/// <summary>
	/// Retrieves a specific event from the calendar store on the device.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to retrieve.</param>
	/// <returns>A <see cref="CalendarEvent"/> object that represents the requested event from the user's device.</returns>
	/// <exception cref="ArgumentException">Thrown when an event with the value specified in <paramref name="eventId"/> could not be found.</exception>
	Task<CalendarEvent> GetEventAsync(string eventId);
}