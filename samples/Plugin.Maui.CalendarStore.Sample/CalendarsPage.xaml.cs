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
		var calendars = await calendarStore.GetCalendars();

		Calendars.Clear();
		foreach(var calendar in calendars)
		{
			Calendars.Add(calendar);
		}
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

		await DisplayAlert("Calendar Created",
			$"Calendar \"{createResult}\" created successfully.", "OK");
	}
}
