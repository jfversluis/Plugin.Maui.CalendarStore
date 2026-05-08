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
	/// <param name="accountName">The account name providing this calendar.</param>
	/// <param name="name">The (display) name for this calendar.</param>
	/// <param name="color">The color associated with this calendar.</param>
	/// <param name="isReadOnly">Indicates whether this calendar is read-only.</param>
	public Calendar(string id, string accountName, string name, Color color, bool isReadOnly)
	{
		Id = id;
		AccountName = accountName;
		Name = name;
		Color = color;
		IsReadOnly = isReadOnly;
	}

	/// <summary>
	/// Gets unique identifier for this calendar.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the account name for this calendar.
	/// </summary>
	public string AccountName { get; }

	/// <summary>
	/// Gets the (display) name for this calendar.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the color associated with this calendar.
	/// </summary>
	public Color Color { get; }

	/// <summary>
	/// Get whether this calendar is read-only.
	/// </summary>
	/// <remarks>Read-only applies to being able to add, edit or delete events for this calendar.</remarks>
	public bool IsReadOnly { get; }
}