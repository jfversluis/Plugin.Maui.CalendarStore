namespace Plugin.Maui.Calendar;

public class CalendarEvent
{
    public CalendarEvent()
    {
    }

    public CalendarEvent(string id, string calendarId, string title)
    {
        Id = id;
        CalendarId = calendarId;
        Title = title;
    }

    public string Id { get; set; }

    public string CalendarId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Location { get; set; }

    public bool AllDay { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }

    public TimeSpan Duration =>
        AllDay ? TimeSpan.FromDays(1) : EndDate - StartDate;

    public IEnumerable<CalendarEventAttendee> Attendees { get; set; }
}