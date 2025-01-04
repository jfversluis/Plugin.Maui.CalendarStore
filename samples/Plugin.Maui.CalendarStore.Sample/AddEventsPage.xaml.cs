using System.Collections.ObjectModel;

namespace Plugin.Maui.CalendarStore.Sample;

public partial class AddEventsPage : ContentPage
{
	readonly CalendarEvent? eventToUpdate;
	readonly ICalendarStore calendarStore;

	public bool IsCreateAction => eventToUpdate is null;
	public ObservableCollection<Calendar> Calendars { get; set; } = [];
	public Calendar? SelectedCalendar { get; set; }
	public string EventTitle { get; set; } = string.Empty;
	public string EventDescription { get; set; } = string.Empty;
	public string EventLocation { get; set; } = string.Empty;
	public DateTime EventStartDate { get; set; } = DateTime.Now;
	public TimeSpan EventStartTime { get; set; } = DateTime.Now.TimeOfDay;
	public DateTime EventEndDate { get; set; } = DateTime.Now;
	public TimeSpan EventEndTime { get; set; } = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));
	public ObservableCollection<Reminder> EventReminders { get; set; } = [];
	public bool EventIsAllDay { get; set; }
	public bool EventHasReminder { get; set; }

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
			EventReminders = new ObservableCollection<Reminder>(eventToUpdate.Reminders);
			SelectedCalendar = Calendars
				.Where(c => c.Id.Equals(eventToUpdate.CalendarId)).Single();

			if (EventHasReminder)
			{
				IsAllDaySection.IsVisible = false;
			}

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
			OnPropertyChanged(nameof(EventHasReminder));
			OnPropertyChanged(nameof(EventReminders));

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

			string savedEventId = string.Empty;

			// Check if we're updating an event
			if (eventToUpdate is not null)
			{
				await calendarStore.UpdateEvent(eventToUpdate.Id, EventTitle, EventDescription,
					EventLocation, startDateTime, startEndDateTime, EventIsAllDay, EventReminders.ToArray());

				await DisplayAlert("Event saved", "The event has been successfully updated!", "OK");
			}
			else
			{
				savedEventId = await calendarStore.CreateEvent(SelectedCalendar.Id, EventTitle,
					EventDescription, EventLocation, startDateTime, startEndDateTime, EventIsAllDay, EventReminders.ToArray());


				await DisplayAlert("Event saved", $"The event has been successfully saved with ID: {savedEventId}!", "OK");
			}

			await Shell.Current.Navigation.PopAsync();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}

	public void AddReminderBtn_Clicked(object sender, EventArgs e)
	{
		if (OperatingSystem.IsWindows() && EventReminders.Count == 1)
		{
			DisplayAlert("Error", "Windows only supports 1 reminder per event.", "OK");
			return;
		}
		
		if (EventStartTime.CompareTo(ReminderTime.Time) <= 0)
		{
			DisplayAlert("Error", "Reminder time has to be earlier than event start time.", "OK");
			return;
		}

		var reminderDateTime = new DateTime(EventStartDate.Year, EventStartDate.Month, EventStartDate.Day,
			ReminderTime.Time.Hours, ReminderTime.Time.Minutes, ReminderTime.Time.Seconds);
		 
		Reminder newReminder = new(reminderDateTime);

		EventReminders.Add(newReminder);
	}

	public void DltReminderBtn_Clicked(object sender, EventArgs e)
	{
		var send = (Button)sender;
		var _reminder = (Reminder)send.BindingContext;
		EventReminders.Remove(_reminder);
		OnPropertyChanged(nameof(EventReminders));
	}

	void ClearRemindersButton_Clicked(object sender, EventArgs e)
	{
		EventReminders.Clear();
	}
}
