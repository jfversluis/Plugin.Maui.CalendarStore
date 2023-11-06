using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace Plugin.Maui.CalendarStore;

/// <summary>
/// The CalendarStore API lets a user access information about the device's calendar and associated data.
/// </summary>
public interface ICalendarStore
{
	/// <summary>
	/// Retrieves all available calendars from the device.
	/// </summary>
	/// <returns>A list of <see cref="Calendar"/> objects that each represent a calendar on the user's device.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	Task<IEnumerable<Calendar>> GetCalendars();

	/// <summary>
	/// Retrieves a specific calendar from the device.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to retrieve.</param>
	/// <returns>A <see cref="Calendar"/> object that represents the requested calendar from the user's device.</returns>
	/// <remarks>The ID format differentiates between platforms. For example, on Android it is an integer, on other platforms it can be a string or GUID.</remarks>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the calendar corresponding with the value in <paramref name="calendarId"/> cannot be found.</exception>
	Task<Calendar> GetCalendar(string calendarId);

	/// <summary>
	/// Creates a new calendar.
	/// </summary>
	/// <param name="name">The name for the calendar to create.</param>
	/// <param name="color">The color to use for the calendar to create.</param>
	/// <returns>The unique identifier of the newly created calendar.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	Task<string> CreateCalendar(string name, Color? color = null);

	/// <summary>
	/// Updates an existing calendar with the provided values.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the existing calendar.</param>
	/// <param name="newName">The new name to update the calendar with.</param>
	/// <param name="newColor">The new color to update the calendar with.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the calendar corresponding with the value in <paramref name="calendarId"/> cannot be found.</exception>
	Task UpdateCalendar(string calendarId, string newName, Color? newColor = null);

	///// <summary>
	///// Deletes a calendar, specified by its unique ID, from the device.
	///// </summary>
	///// <param name="calendarId">The unique identifier of the calendar to be deleted.</param>
	///// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	///// <remarks>If the calendar is part of a cloud service, the calendar might also be deleted from all other devices where the calendar is used.</remarks>
	//Task DeleteCalendar(string calendarId);

	///// <summary>
	///// Deletes the given calendar from the device.
	///// </summary>
	///// <param name="calendarToDelete">The calendar object that is to be deleted.</param>
	///// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	///// <remarks>If the calendar is part of a cloud service, the calendar might also be deleted from all other devices where the calendar is used.</remarks>
	//Task DeleteCalendar(Calendar calendarToDelete);

	/// <summary>
	/// Retrieves events from a specific calendar or all calendars from the device.
	/// </summary>
	/// <param name="calendarId">The calendar identifier to retrieve events for. If not provided, events will be retrieved for all calendars on the device.</param>
	/// <param name="startDate">The start date of the range to retrieve events for.</param>
	/// <param name="endDate">The end date of the range to retrieve events for.</param>
	/// <returns>A list of events from the calendars on the device.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when a calendar with the value specified in <paramref name="calendarId"/> could not be found.</exception>
	Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null,
		DateTimeOffset? startDate = null, DateTimeOffset? endDate = null);

	/// <summary>
	/// Retrieves a specific event from the calendar store on the device.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to retrieve.</param>
	/// <returns>A <see cref="CalendarEvent"/> object that represents the requested event from the user's device.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event corresponding with the value in <paramref name="eventId"/> cannot be found.</exception>
	Task<CalendarEvent> GetEvent(string eventId);

	/// <summary>
	/// Creates a new event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to add the newly created event to.</param>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="location">The location of the event.</param>
	/// <param name="startDateTime">The start date and time for the event.</param>
	/// <param name="endDateTime">The end date and time for the event.</param>
	/// <param name="isAllDay">Indicates whether or not this event should be marked as an all-day event.</param>
	/// <returns>The unique identifier of the newly created event.</returns>
	/// <remarks>When <paramref name="isAllDay"/> is set to <see langword="true"/>, any time information in <paramref name="startDateTime"/> and <paramref name="endDateTime"/> is omitted.</remarks>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the calendar corresponding with the value in <paramref name="calendarId"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task<string> CreateEvent(string calendarId, string title, string description, string location,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false);


	/// <summary>
	/// Creates a new event with reminder with the provided information on specified calendar and reminder time
	/// </summary> 
	/// /// <param name="calendarId">The unique identifier of the calendar to add the newly created event to.</param>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="location">The location of the event.</param>
	/// <param name="startDateTime">The start date and time for the event.</param>
	/// <param name="endDateTime">The end date and time for the event.</param>
	/// <param name="reminderMinutes">The number of minutes before event starts that reminder notification will be show</param>
	/// <returns>The unique identifier of the newly created event.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the calendar corresponding with the value in <paramref name="calendarId"/> and or <paramref name="reminderMinutes"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	///
	Task<string> CreateEventWithReminder(string calendarId, string title, string description, string location,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, int reminderMinutes, bool isAllDay = false);
	/// <summary>
	/// Creates a new event based on the provided <paramref name="calendarEvent"/> object.
	/// </summary>
	/// <param name="calendarEvent">The event object with the details to save to the calendar specified in this object.</param>
	/// <returns>The unique identifier of the newly created event.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>


	Task<string> CreateEvent(CalendarEvent calendarEvent);

	/// <summary>
	/// Creates a new all day event with the provided information in the specified calendar.
	/// </summary>
	/// <param name="calendarId">The unique identifier of the calendar to save the event in.</param>
	/// <param name="title">The title of the event.</param>
	/// <param name="description">The description of the event.</param>
	/// <param name="location">The location of the event.</param>
	/// <param name="startDate">The start date for the event.</param>
	/// <param name="endDate">The end date for the event.</param>
	/// <returns>The unique identifier of the newly created event.</returns>
	/// <remarks>Any time information in <paramref name="startDate"/> and <paramref name="endDate"/> is omitted.</remarks>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate);

	/// <summary>
	/// Updates an existing event with the provided information.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to update.</param>
	/// <param name="title">The updated title for the event.</param>
	/// <param name="description">The updated description for the event.</param>
	/// <param name="location">The updated location for the event.</param>
	/// <param name="startDateTime">The updated start date and time for the event.</param>
	/// <param name="endDateTime">The updated end date and time for the event.</param>
	/// <param name="isAllDay">The updated value that indicates whether or not this event should be marked as an all-day event.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event corresponding with the value in <paramref name="eventId"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay);

	/// <summary>
	/// Updates an existing event with the provided information.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to update.</param>
	/// <param name="title">The updated title for the event.</param>
	/// <param name="description">The updated description for the event.</param>
	/// <param name="location">The updated location for the event.</param>
	/// <param name="startDateTime">The updated start date and time for the event.</param>
	/// <param name="endDateTime">The updated end date and time for the event.</param>
	/// <param name="isAllDay">The updated value that indicates whether or not this event should be marked as an all-day event.</param>
	/// <param name="reminderMinutes">The updated value if any for the reminder minutes.
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event corresponding with the value in <paramref name="eventId"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task UpdateEventWithReminder(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, int reminderMinutes);

	/// <summary>
	/// Updates a event based on the provided <paramref name="eventToUpdate"/> object. 
	/// </summary>
	/// <param name="eventToUpdate">The event object with the details to update the existing event with.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event identifier corresponding with the value in <paramref name="eventToUpdate"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>

	Task UpdateEvent(CalendarEvent eventToUpdate);

	/// <summary>
	/// Deletes an event, specified by its unique ID, from the device calendar.
	/// </summary>
	/// <param name="eventId">The unique identifier of the event to be deleted.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event corresponding with the value in <paramref name="eventId"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task DeleteEvent(string eventId);

	/// <summary>
	/// Deletes the given event from the device calendar.
	/// </summary>
	/// <param name="eventToDelete">The event object that is to be deleted.</param>
	/// <returns>A <see cref="Task"/> object with the current status of the asynchronous operation.</returns>
	/// <exception cref="PermissionException">Thrown when the permission to access the calendar is not granted.</exception>
	/// <exception cref="ArgumentException">Thrown when the event identifier corresponding with the value in <paramref name="eventToDelete"/> cannot be found.</exception>
	/// <exception cref="CalendarStore.CalendarStoreException">Thrown for a variety of reasons, the exception will hold more information.</exception>
	Task DeleteEvent(CalendarEvent eventToDelete);
}