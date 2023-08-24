namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Represents an attendee that is part of a calendar event from the device's calendar store.
/// </summary>
/// <remarks>On iOS <see cref="Email"/> is equal to <see cref="Name"/> as there is no obvious way to retrieve the email data from an attendee.</remarks>
public class CalendarEventAttendee
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CalendarEventAttendee"/> class.
	/// </summary>
	/// <param name="name">The name of the attendee.</param>
	/// <param name="email">The email address associated to the attendee.</param>
	public CalendarEventAttendee(string name, string email)
    {
        Name = name;
        Email = email;
    }

	/// <summary>
	/// Gets the name of this attendee.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the email address of this attendee.
	/// </summary>
    public string Email { get; }
}