using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

// This page uses dependency injection and injects the ICalendarStore
// implementation that is registered in MauiProgram.cs
public partial class CalendarsPage : ContentPage
{
	readonly ICalendarStore calendarStore;

	public ObservableCollection<Calendar> Calendars { get; set; } = new();

	public CalendarsPage(ICalendarStore calendarStore)
	{
		BindingContext = this;

		InitializeComponent();

		this.calendarStore = calendarStore;
	}

	async void LoadCalendars_Clicked(object sender, EventArgs e)
	{
		await LoadCalendars();
	}

	async void CreateCalendar_Clicked(object sender, EventArgs e)
	{
		var createResult = await DisplayPromptAsync("Create Calendar",
			"What do you want to name the calendar?");

		if (string.IsNullOrWhiteSpace(createResult))
		{
			return;
		}

		await calendarStore.CreateCalendar(createResult);

		await LoadCalendars();

		await DisplayAlert("Calendar Created",
			$"Calendar \"{createResult}\" created successfully.", "OK");
	}

	async void Update_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not Calendar calendarToUpdate)
		{
			await DisplayAlert("Error", "Could not determine calendar to update.", "OK");
			return;
		}

		var updateResult = await DisplayPromptAsync("Update Calendar",
			"Enter the updated calendar name:", placeholder: calendarToUpdate.Name);

		if (string.IsNullOrWhiteSpace(updateResult))
		{
			return;
		}

		await calendarStore.UpdateCalendar(calendarToUpdate.Id, updateResult);

		await LoadCalendars();

		await DisplayAlert("Calendar Updated",
			$"Calendar \"{updateResult}\" updated successfully.", "OK");
	}

	async void Delete_Clicked(object sender, EventArgs e)
	{
		if ((sender as BindableObject)?.
			BindingContext is not Calendar calendarToRemove)
		{
			await DisplayAlert("Error", "Could not determine calendar to delete.", "OK");
			return;
		}

		var promptResult = await DisplayActionSheet(
			$"Are you sure you want to delete calendar \"{calendarToRemove.Name}\"?",
			"Cancel", "Remove");

		if (promptResult.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		await CalendarStore.Default.DeleteCalendar(calendarToRemove.Id);
		Calendars.Remove(calendarToRemove);

		await DisplayAlert("Success", "Calendar deleted!", "OK");
	}

	async Task LoadCalendars()
	{
		var calendars = await calendarStore.GetCalendars();

		Calendars.Clear();
		foreach (var calendar in calendars)
		{
			Calendars.Add(calendar);
		}
	}
}
