using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

public partial class AddEventsPage : ContentPage
{
	readonly CalendarEvent? eventToUpdate;
	readonly ICalendarStore calendarStore;

	public bool IsCreateAction => eventToUpdate is null;

	public ObservableCollection<Calendar> Calendars { get; set; } = new();
	public Calendar? SelectedCalendar { get; set; }
	public string EventTitle { get; set; } = string.Empty;
	public string EventDescription { get; set; } = string.Empty;
	public string EventLocation { get; set; } = string.Empty;
	public DateTime EventStartDate { get; set; } = DateTime.Now;
	public TimeSpan EventStartTime { get; set; } = DateTime.Now.TimeOfDay;
	public DateTime EventEndDate { get; set; } = DateTime.Now;
	public TimeSpan EventEndTime { get; set; } = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));
	public bool EventIsAllDay { get; set; }

	public AddEventsPage(ICalendarStore calendarStore, CalendarEvent? eventToUpdate)
	{
		BindingContext = this;

		InitializeComponent();

		this.calendarStore = calendarStore;
		this.eventToUpdate = eventToUpdate;
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

		if (eventToUpdate is not null)
		{
			EventTitle = eventToUpdate.Title;
			EventDescription = eventToUpdate.Description;
			EventLocation = eventToUpdate.Location;
			EventStartDate = eventToUpdate.StartDate.LocalDateTime;
			EventStartTime = eventToUpdate.StartDate.LocalDateTime.TimeOfDay;
			EventEndDate = eventToUpdate.EndDate.LocalDateTime;
			EventEndTime = eventToUpdate.EndDate.LocalDateTime.TimeOfDay;
			EventIsAllDay = eventToUpdate.IsAllDay;

			SelectedCalendar = Calendars
				.Where(c => c.Id.Equals(eventToUpdate.CalendarId)).Single();

			OnPropertyChanged(nameof(EventTitle));
			OnPropertyChanged(nameof(EventDescription));
			OnPropertyChanged(nameof(EventLocation));
			OnPropertyChanged(nameof(EventStartDate));
			OnPropertyChanged(nameof(EventStartTime));
			OnPropertyChanged(nameof(EventEndDate));
			OnPropertyChanged(nameof(EventEndTime));
			OnPropertyChanged(nameof(EventIsAllDay));
			OnPropertyChanged(nameof(SelectedCalendar));
			OnPropertyChanged(nameof(IsCreateAction));

			Title = "Edit Event";
		}
	}

	async void Save_Clicked(object sender, EventArgs e)
	{
		try
		{
			if (SelectedCalendar is null)
			{
				await DisplayAlert("Error", "No calendar selected. Select a calendar and try again.", "OK");

				return;
			}

			if (SelectedCalendar.IsReadOnly)
			{
				await DisplayAlert("Error", "The selected calendar is read-only.", "OK");

				return;
			}

			var startDateTime = new DateTime(EventStartDate.Year, EventStartDate.Month,
				EventStartDate.Day, EventStartTime.Hours, EventStartTime.Minutes, EventStartTime.Seconds);

			var startEndDateTime = new DateTime(EventEndDate.Year, EventEndDate.Month,
				EventEndDate.Day, EventEndTime.Hours, EventEndTime.Minutes, EventEndTime.Seconds);

			// Check if we're updating an event
			if (eventToUpdate is not null)
			{
				await calendarStore.UpdateEvent(eventToUpdate.Id, EventTitle, EventDescription,
					EventLocation, startDateTime, startEndDateTime, EventIsAllDay);
			}
			else
			{
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
			}

			await DisplayAlert("Event saved", "The event has been successfully saved!", "OK");

			await Shell.Current.Navigation.PopAsync();
		}
		catch(Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
}
