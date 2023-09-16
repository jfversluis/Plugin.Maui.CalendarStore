using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

public partial class AddEventsPage : ContentPage
{
	readonly ICalendarStore calendarStore;

	public ObservableCollection<Calendar> Calendars { get; set; } = new();

	public Calendar? SelectedCalendar { get; set; }

	public string EventTitle { get; set; } = string.Empty;

	public string EventDescription { get; set; } = string.Empty;

	public string EventLocation { get; set; } = string.Empty;

	public DateTime EventStartDate { get; set; } = DateTime.Now;
	public TimeSpan EventStartTime { get; set; } = DateTime.Now.TimeOfDay;

	public DateTime EventEndDate { get; set; } = DateTime.Now.AddDays(1);
	public TimeSpan EventEndTime { get; set; } = DateTime.Now.AddDays(1).TimeOfDay;

	public bool EventIsAllDay { get; set; }

	public AddEventsPage(ICalendarStore calendarStore)
	{
		BindingContext = this;

		InitializeComponent();

		this.calendarStore = calendarStore;
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		if (Calendars.Count > 0)
		{
			return;
		}

		foreach (var calendar in await calendarStore.GetCalendars())
		{
			Calendars.Add(calendar);
		}
	}

	async void Button_Clicked(object sender, EventArgs e)
	{
		try
		{
			if (SelectedCalendar is null)
			{
				await DisplayAlert("Error", "No calendar selected. Select a calendar and try again.", "OK");

				return;
			}

			var startDateTime = new DateTime(EventStartDate.Year, EventStartDate.Month,
				EventStartDate.Day, EventStartTime.Hours, EventStartTime.Minutes, EventStartTime.Seconds);

			var startEndDateTime = new DateTime(EventEndDate.Year, EventEndDate.Month,
				EventEndDate.Day, EventEndTime.Hours, EventEndTime.Minutes, EventEndTime.Seconds);

			if (EventIsAllDay)
			{
				await calendarStore.CreateAllDayEvent(SelectedCalendar.Id, EventTitle,
					EventDescription, EventLocation, startDateTime, startEndDateTime);
			}
			else
			{
				await calendarStore.CreateEvent(SelectedCalendar.Id, EventTitle,
					EventDescription, EventLocation, startDateTime, startEndDateTime);
			}

			await DisplayAlert("Event saved", "The event has been successfully saved!", "OK");
		}
		catch(Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
}
