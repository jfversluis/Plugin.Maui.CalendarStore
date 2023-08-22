namespace Plugin.Maui.Calendar;

public class CalendarEventAttendee
{
    public CalendarEventAttendee()
    {
    }

    public CalendarEventAttendee(string name, string email)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; set; }

    public string Email { get; set; }
}