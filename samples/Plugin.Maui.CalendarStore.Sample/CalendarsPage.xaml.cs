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

	async void Button_Clicked_Calendars(object sender, EventArgs e)
	{
		var calendars = await calendarStore.GetCalendars();

		Calendars.Clear();
		foreach(var calendar in calendars)
		{
			Calendars.Add(calendar);
		}
	}
}
