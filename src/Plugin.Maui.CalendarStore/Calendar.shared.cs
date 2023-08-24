namespace Plugin.Maui.CalendarStore;

/// <summary>
/// Represents a calendar from the device's calendar store.
/// </summary>
public class Calendar
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Calendar"/> class.
	/// </summary>
	/// <param name="id">The unique identifier for this calendar.</param>
	/// <param name="name">The (display) name for this calendar.</param>
	public Calendar(string id, string name)
    {
        Id = id;
        Name = name;
    }

	/// <summary>
	/// Gets unique identifier for this calendar.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the (display) name for this calendar.
	/// </summary>
	public string Name { get; }
}