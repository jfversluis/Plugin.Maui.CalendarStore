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

	async void Delete_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not CalendarEvent eventToRemove)
		{
			await DisplayAlert("Error", "Could not determine event to delete.", "OK");
			return;
		}

		var promptResult = await DisplayActionSheet(
			$"Are you sure you want to delete event \"{eventToRemove.Title}\"?",
			"Cancel", "Remove");

		if (promptResult.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		await CalendarStore.Default.DeleteEvent(eventToRemove.Id);
		Events.Remove(eventToRemove);

		await DisplayAlert("Success", "Event deleted!", "OK");
	}
}