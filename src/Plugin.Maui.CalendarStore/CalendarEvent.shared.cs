namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Represents an event that is part of a calendar from the device's calendar store.
/// </summary>
public class CalendarEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CalendarEvent"/> class.
	/// </summary>
	/// <param name="id">The unique identifier for this event.</param>
	/// <param name="calendarId">The unique identifier for the calendar this event is part of.</param>
	/// <param name="title">The title for this event.</param>
	public CalendarEvent(string id, string calendarId, string title)
    {
        Id = id;
        CalendarId = calendarId;
        Title = title;
    }

	/// <summary>
	/// Gets the unique identifier for this event.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the unique identifier for the calendar this event is part of.
	/// </summary>
	public string CalendarId { get; }

	/// <summary>
	/// Gets the title for this event.
	/// </summary>
	public string Title { get; }

	/// <summary>
	/// Gets the description for this event.
	/// </summary>
	public string Description { get; internal set; } = string.Empty;

	/// <summary>
	/// Gets the location for this event.
	/// </summary>
	public string Location { get; internal set; } = string.Empty;

	/// <summary>
	/// Gets whether this event is marked as an all-day event.
	/// </summary>
    public bool IsAllDay { get; internal set; }

	/// <summary>
	/// Gets the start date and time for this event.
	/// </summary>
    public DateTimeOffset StartDate { get; internal set; }

	/// <summary>
	/// Gets the end date and time for this event.
	/// </summary>
    public DateTimeOffset EndDate { get; internal set; }

	/// <summary>
	/// Gets the total duration for this event.
	/// </summary>
    public TimeSpan Duration => EndDate - StartDate;

	/// <summary>
	/// Gets the list of reminders for this event.
	/// </summary>
	/// <remarks>
	/// On Windows only 1 reminder is supported. Therefore on Windows, this collection will always only contain 1 item.
	/// </remarks>
	public List<Reminder> Reminders { get; internal set;} = [];

	/// <summary>
	/// Gets whether this event has at least one reminder.
	/// </summary>
	public bool HasReminders => Reminders?.Count > 0;

	/// <summary>
	/// Gets the list of attendees for this event.
	/// </summary>
	public IEnumerable<CalendarEventAttendee> Attendees { get; internal set; } = [];
}