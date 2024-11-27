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
	public ObservableCollection<Reminder> EventReminders { get; set; } = new();
	public bool EventIsAllDay { get; set; }
	public bool EventHasReminder { get; set; }
	public int MinsBeforeReminder { get; set; }

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
			EventHasReminder = eventToUpdate.Reminders.Count > 0;
			EventReminders =new ObservableCollection<Reminder>(eventToUpdate.Reminders);
			//MinsBeforeReminder = eventToUpdate.MinutesBeforeReminder;
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
			OnPropertyChanged(nameof(MinsBeforeReminder));

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
				if (HasReminder)
				{
					//best to add another check here to confirm if user wants to save with reminder - for this example

					await calendarStore.UpdateEventWithReminder(eventToUpdate.Id, EventTitle, EventDescription,
						EventLocation, startDateTime, startEndDateTime, EventIsAllDay, MinsBeforeReminder);
				}
				else
				{
					await calendarStore.UpdateEvent(eventToUpdate.Id, EventTitle, EventDescription,
						EventLocation, startDateTime, startEndDateTime, EventIsAllDay);
				}

				await DisplayAlert("Event saved", $"The event has been successfully updated!", "OK");
			}
			else
			{
				// We could also not use this overload and just provide EventIsAllDay as a parameter
				if (EventIsAllDay)
				{
					savedEventId = await calendarStore.CreateAllDayEvent(SelectedCalendar.Id, EventTitle,
						EventDescription, EventLocation, startDateTime, startEndDateTime);
				}
				if(HasReminder)
				{
					savedEventId = await calendarStore.CreateEventWithReminder(SelectedCalendar.Id, EventTitle,
						EventDescription, EventLocation, startDateTime, startEndDateTime, MinsBeforeReminder);
				}
				else
				{
					savedEventId = await calendarStore.CreateEvent(SelectedCalendar.Id, EventTitle,
						EventDescription, EventLocation, startDateTime, startEndDateTime, reminders: EventReminders.ToArray());
				}

				await DisplayAlert("Event saved", $"The event has been successfully saved with ID: {savedEventId}!", "OK");
			}

			await Shell.Current.Navigation.PopAsync();
		}
		catch(Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}

	void HasReminderChkbx_CheckedChanged(object sender, CheckedChangedEventArgs e)
	{
		IsAllDaySection.IsVisible=!e.Value;
	}

	
	void ReminderMinsEntry_Focused(object sender, FocusEventArgs e)
	{
		if (MinsBeforeReminder == 0)
		{
			ReminderMinsEntry.Text = "";
			
		}
		else
		{
			ReminderMinsEntry.Text = MinsBeforeReminder.ToString();
		}
	}

	public void AddReminderBtn_Clicked(object sender, EventArgs e)
	{
		//EventReminders.Clear(); just for easy demo
		if (!string.IsNullOrEmpty(ReminderMinsEntry.Text))
		{
			var ReminderMins = int.Parse(ReminderMinsEntry.Text);
			Reminder newReminder = new Reminder(GetEventEndDateTime(EventStartDate, EventStartTime).AddMinutes(-ReminderMins));
			newReminder.ReminderInMinutes = ReminderMins;
			EventReminders.Add(newReminder);
			OnPropertyChanged(nameof(EventReminders));
			ReminderMinsEntry.Text= string.Empty;
			
		}
	}

	public void ReminderMinsEntry_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (AddReminderBtn is null)
		{
			return;
		}
		if(!string.IsNullOrEmpty(e.NewTextValue))
		{
			AddReminderBtn.IsVisible = true; 
		}
		else
		{
			AddReminderBtn.IsVisible = false;
		}
	}

	public void RemindersColView_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{

		if (RemindersColView.SelectedItem is Reminder selectedReminder)
		{
			// Combine EventEndDate and EventEndTime
			DateTimeOffset eventEndDateTime = GetEventEndDateTime(EventEndDate, EventEndTime);

			// Calculate minutes before the event end
			int minutesBeforeEnd = (int)(eventEndDateTime - selectedReminder.DateTime).TotalMinutes;

			// Set the text in ReminderMinsEntry
			ReminderMinsEntry.Text = minutesBeforeEnd >= 0
				? minutesBeforeEnd.ToString()
				: "Reminder is past the event end time.";
		}
		else
		{
			ReminderMinsEntry.Text = "No reminder selected.";
		}

	}

	DateTimeOffset GetEventEndDateTime(DateTime eventEndDate, TimeSpan eventEndTime)
	{
		return new DateTimeOffset(
			eventEndDate.Year,
			eventEndDate.Month,
			eventEndDate.Day,
			eventEndTime.Hours,
			eventEndTime.Minutes,
			eventEndTime.Seconds,
			DateTimeOffset.Now.Offset // Assume current offset for simplicity
		);
	}

	public void DltReminderBtn_Clicked(object sender, EventArgs e)
	{
		var _reminder = (Reminder)AddReminderBtn.CommandParameter;
		EventReminders.Remove(_reminder);
		OnPropertyChanged(nameof(EventReminders)); //doesn't update the UI hence why i clear up top
	}
}
