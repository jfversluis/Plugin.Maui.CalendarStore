using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

// This page uses the static CalendarStore.Default class
public partial class EventsPage : ContentPage
{
	bool needsReload;

	public ObservableCollection<CalendarEvent> Events { get; set; } = new();

	public EventsPage()
	{
		BindingContext = this;

		InitializeComponent();
	}

	async void LoadEvents_Clicked(object sender, EventArgs e)
	{
		await LoadEvents();
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		if (needsReload)
		{
			await LoadEvents();
		}
	}

	async void CreateEvent_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.Navigation.PushAsync(
			new AddEventsPage(CalendarStore.Default, null));

		needsReload = true;
	}

	async void Update_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not CalendarEvent eventToUpdate)
		{
			await DisplayAlert("Error", "Could not determine event to update.", "OK");
			return;
		}

		await Shell.Current.Navigation.PushAsync(
			new AddEventsPage(CalendarStore.Default, eventToUpdate));

		needsReload = true;
	}

	async void Delete_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not CalendarEvent eventToRemove)
		{
			await DisplayAlert("Error", "Could not determine event to delete.", "OK");
			return;
		}

		var scope = RecurrenceScope.AllEvents;

		if (eventToRemove.IsRecurring)
		{
			var scopeChoice = await DisplayActionSheet(
				$"How much of \"{eventToRemove.Title}\" should be deleted?",
				"Cancel", null, "This occurrence", "All events");

			switch (scopeChoice)
			{
				case "This occurrence":
					scope = RecurrenceScope.ThisEvent;
					break;
				case "All events":
					scope = RecurrenceScope.AllEvents;
					break;
				default:
					return;
			}
		}
		else
		{
			var promptResult = await DisplayActionSheet(
				$"Are you sure you want to delete event \"{eventToRemove.Title}\"?",
				"Cancel", "Remove");

			if (!promptResult.Equals("Remove", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		try
		{
			await CalendarStore.Default.DeleteEvent(eventToRemove.Id, scope,
				eventToRemove.OriginalOccurrenceStart);
		}
		catch (NotSupportedException ex)
		{
			await DisplayAlert("Not supported", ex.Message, "OK");
			return;
		}

		Events.Remove(eventToRemove);

		await DisplayAlert("Success", "Event deleted!", "OK");
	}

	async Task LoadEvents()
	{
		IEnumerable<CalendarEvent>? events = await CalendarStore.Default
			.GetEvents(startDate: DateTimeOffset.Now.AddDays(-7),
			endDate: DateTimeOffset.Now.AddDays(7));

		Events.Clear();
		foreach (var ev in events)
		{
			Events.Add(ev);
		}
	}
}