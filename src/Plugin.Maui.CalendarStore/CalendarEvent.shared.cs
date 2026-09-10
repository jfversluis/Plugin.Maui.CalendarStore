using Microsoft.Maui.Graphics;

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
	public List<Reminder> Reminders { get; internal set; } = [];

	/// <summary>
	/// Gets the display color for this event.
	/// </summary>
	/// <remarks>
	/// On Android, this returns the event's custom color if one is set, otherwise <see langword="null"/>.
	/// On iOS and macOS, this returns the calendar's color (which is the color used to display the event).
	/// On Windows, this will be <see langword="null"/>; use the <see cref="Calendar.Color"/> from the parent calendar instead.
	/// </remarks>
	public Color? EventColor { get; internal set; }

	/// <summary>
	/// Gets the list of attendees for this event.
	/// </summary>
	public IEnumerable<CalendarEventAttendee> Attendees { get; internal set; } = [];

	/// <summary>
	/// Gets the identifier of the time zone this event is anchored to.
	/// </summary>
	/// <remarks>
	/// The value is an IANA time zone identifier (for example <c>America/New_York</c>),
	/// or <see langword="null"/> when the event is floating and follows the device's
	/// local time zone.
	/// </remarks>
	public string? TimeZoneId { get; internal set; }

	/// <summary>
	/// Gets a value indicating whether this event is part of a recurring series.
	/// </summary>
	public bool IsRecurring => Recurrence is not null;

	/// <summary>
	/// Gets the recurrence rule for this event, or <see langword="null"/> when the event does not recur.
	/// </summary>
	public CalendarRecurrence? Recurrence { get; internal set; }

	/// <summary>
	/// Gets a value indicating whether this event is a detached occurrence of a recurring series.
	/// </summary>
	/// <remarks>
	/// A detached occurrence is a single occurrence that has been modified or cancelled
	/// independently of the rest of the series (an exception).
	/// </remarks>
	public bool IsDetached { get; internal set; }

	/// <summary>
	/// Gets the original start date and time of the occurrence within its recurring series.
	/// </summary>
	/// <remarks>
	/// This value identifies a specific occurrence and remains stable even when the occurrence
	/// has been moved by an exception. It is <see langword="null"/> for events that are not
	/// returned as part of a recurring series.
	/// </remarks>
	public DateTimeOffset? OriginalOccurrenceStart { get; internal set; }
}