namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Represents a reminder that notifies the user of an upcoming <see cref="CalendarEvent"/>.
/// </summary>
public class Reminder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Reminder"/> class.
    /// </summary>
    /// <param name="dateTime">The date and time this reminder should notify the user of an <see cref="CalendarEvent"/>.</param>
    public Reminder(DateTimeOffset dateTime)
    {
        DateTime = dateTime;
    }

    public DateTimeOffset DateTime { get; set; }
}