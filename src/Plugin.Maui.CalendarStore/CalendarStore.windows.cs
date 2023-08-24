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

		return calendar == null ? throw CalendarStore.InvalidCalendar(calendarId) : ToCalendar(calendar);
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
		if ((events == null || events.Count() == 0) && !string.IsNullOrEmpty(calendarId))
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

		return e == null ? throw CalendarStore.InvalidEvent(eventId) : ToEvent(e);
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