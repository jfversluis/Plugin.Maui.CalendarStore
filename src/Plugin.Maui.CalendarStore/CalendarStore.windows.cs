using Microsoft.Maui.ApplicationModel;
using Windows.ApplicationModel.Appointments;

namespace Plugin.Maui.CalendarStore;

partial class FeatureImplementation : ICalendarStore
{
	Task<AppointmentStore>? uwpAppointmentStore;

	Task<AppointmentStore> GetInstanceAsync() =>
		uwpAppointmentStore ??= AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadOnly).AsTask();

	/// <inheritdoc/>
	public async Task<IEnumerable<Calendar>> GetCalendars()
	{
		var instance = await GetInstanceAsync().ConfigureAwait(false);

		var calendars = await instance.FindAppointmentCalendarsAsync(FindAppointmentCalendarsOptions.IncludeHidden).AsTask().ConfigureAwait(false);

		return ToCalendars(calendars).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		var instance = await GetInstanceAsync().ConfigureAwait(false);

		var calendar = await instance.GetAppointmentCalendarAsync(calendarId).AsTask().ConfigureAwait(false);

		return calendar is null ? throw CalendarStore.InvalidCalendar(calendarId) : ToCalendar(calendar);
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		var options = new FindAppointmentsOptions();

		// properties
		options.FetchProperties.Add(AppointmentProperties.Subject);
		options.FetchProperties.Add(AppointmentProperties.Details);
		options.FetchProperties.Add(AppointmentProperties.Location);
		options.FetchProperties.Add(AppointmentProperties.StartTime);
		options.FetchProperties.Add(AppointmentProperties.Duration);
		options.FetchProperties.Add(AppointmentProperties.AllDay);
		options.FetchProperties.Add(AppointmentProperties.Invitees);

		// calendar
		if (!string.IsNullOrEmpty(calendarId))
		{
			options.CalendarIds.Add(calendarId);
		}

		// dates
		var sDate = startDate ?? DateTimeOffset.Now.Add(CalendarStore.defaultStartTimeFromNow);
		var eDate = endDate ?? sDate.Add(CalendarStore.defaultEndTimeFromStartTime);
		
		if (eDate < sDate)
		{
			eDate = sDate;
		}

		var instance = await GetInstanceAsync().ConfigureAwait(false);

		var events = await instance.FindAppointmentsAsync(sDate, eDate.Subtract(sDate), options).AsTask().ConfigureAwait(false);

		// confirm the calendar exists if no events were found
		// the PlatformGetCalendarAsync will throw if not
		if ((events is null || events.Count == 0) && !string.IsNullOrEmpty(calendarId))
		{
			await GetCalendar(calendarId).ConfigureAwait(false);
		}

		return ToEvents(events.OrderBy(e => e.StartTime)).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId)
	{
		var instance = await GetInstanceAsync().ConfigureAwait(false);

		var e = await instance.GetAppointmentAsync(eventId).AsTask().ConfigureAwait(false);

		return e is null ? throw CalendarStore.InvalidEvent(eventId) : ToEvent(e);
	}

	/// <inheritdoc/>
	public Task CreateAllDayEvent(string calendarId, string title, string description,
		DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return InternalSaveEvent(calendarId, title, description, startDate, endDate, true);
	}

	/// <inheritdoc/>
	public Task CreateEvent(string calendarId, string title, string description,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime)
	{
		return InternalSaveEvent(calendarId, title, description, startDateTime, endDateTime);
	}

	/// <inheritdoc/>
	public Task CreateEvent(CalendarEvent calendarEvent)
	{
		return InternalSaveEvent(calendarEvent.CalendarId, calendarEvent.Title,
			calendarEvent.Description, calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.AllDay);
	}

	static async Task InternalSaveEvent(string calendarId, string title, string description,
		DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDayEvent = false)
	{
		var permissionResult = await Permissions.RequestAsync<Permissions.CalendarWrite>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException("Permission for writing to calendar store is not granted.");
		}

		var platformCalendarManager = await AppointmentManager
			.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadWrite);

		var platformCalendar = await platformCalendarManager
			.GetAppointmentCalendarAsync(calendarId);

		var eventToSave = new Appointment
		{
			Subject = title,
			Details = description,
			StartTime = startDateTime.LocalDateTime,
			Duration = endDateTime.Subtract(startDateTime),
			AllDay = isAllDayEvent,
		};

		await platformCalendar.SaveAppointmentAsync(eventToSave);
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<AppointmentCalendar> native)
	{
		foreach (var calendar in native)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(AppointmentCalendar calendar) =>
		new(calendar.LocalId, calendar.DisplayName);

	static IEnumerable<CalendarEvent> ToEvents(IEnumerable<Appointment> native)
	{
		foreach (var e in native)
		{
			yield return ToEvent(e);
		}
	}

	static CalendarEvent ToEvent(Appointment e) =>
		new(e.LocalId, e.CalendarId, e.Subject)
		{
			Description = e.Details,
			Location = e.Location,
			StartDate = e.StartTime,
			AllDay = e.AllDay,
			EndDate = e.StartTime.Add(e.Duration),
			Attendees = e.Invitees != null
				? ToAttendees(e.Invitees).ToList()
				: new List<CalendarEventAttendee>()
		};

	static IEnumerable<CalendarEventAttendee> ToAttendees(IEnumerable<AppointmentInvitee> native)
	{
		foreach (var attendee in native)
		{
			yield return new(attendee.DisplayName, attendee.Address);
		}
	}
}