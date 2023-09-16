using Microsoft.Maui.Graphics;

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
	/// <param name="name">The color associated with this calendar.</param>
	public Calendar(string id, string name, Color color)
    {
        Id = id;
        Name = name;
		Color = color;
    }

	/// <summary>
	/// Gets unique identifier for this calendar.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the (display) name for this calendar.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the color associated with this calendar.
	/// </summary>
	public Color Color { get; }
}