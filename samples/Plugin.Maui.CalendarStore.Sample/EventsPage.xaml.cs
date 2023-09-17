using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

// This page uses the static CalendarStore.Default class
public partial class EventsPage : ContentPage
{
	public ObservableCollection<CalendarEvent> Events { get; set; } = new();

	public EventsPage()
	{
		BindingContext = this;

		InitializeComponent();
	}

	async void LoadEvents_Clicked(object sender, EventArgs e)
	{
		var events = await CalendarStore.Default.GetEvents(startDate: DateTimeOffset.Now.AddDays(-7),
			endDate: DateTimeOffset.Now.AddDays(7));

		Events.Clear();
		foreach(var ev in events)
		{
			Events.Add(ev);
		}
	}

	async void Remove_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not CalendarEvent eventToRemove)
		{
			await DisplayAlert("Error", "Could not determine event to remove.", "OK");
			return;
		}

		var promptResult = await DisplayActionSheet(
			$"Are you sure you want to remove event \"{eventToRemove.Title}\"?{Environment.NewLine}" +
			$"{Environment.NewLine}This will result in the event being actually deleted from your actual calendar.",
			"Cancel", "Remove");

		if (promptResult.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		await CalendarStore.Default.RemoveEvent(eventToRemove.Id);
		Events.Remove(eventToRemove);

		await DisplayAlert("Success", "Event removed!", "OK");
	}
}