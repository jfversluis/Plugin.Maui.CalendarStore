using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Windows.ApplicationModel.Appointments;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	Task<AppointmentStore>? uwpAppointmentStore;

	Task<AppointmentStore> GetAppointmentStore(bool requestWrite = false) =>
		uwpAppointmentStore ??= AppointmentManager.RequestStoreAsync(
			requestWrite ? AppointmentStoreAccessType.AllCalendarsReadWrite
			: AppointmentStoreAccessType.AllCalendarsReadOnly).AsTask();

	/// <inheritdoc/>
	public async Task<IEnumerable<Calendar>> GetCalendars()
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var instance = await GetAppointmentStore().ConfigureAwait(false);

		var calendars = await instance.FindAppointmentCalendarsAsync(
			FindAppointmentCalendarsOptions.IncludeHidden)
			.AsTask().ConfigureAwait(false);

		return ToCalendars(calendars).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendar = await GetPlatformCalendar(calendarId);

		return calendar is null ?
			throw CalendarStore.InvalidCalendar(calendarId) : ToCalendar(calendar);
	}

	/// <inheritdoc/>
	public async Task CreateCalendar(string name, Color? color = null)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendarManager = await GetAppointmentStore(true)
			.ConfigureAwait(false);

		var platformCalendar = 
			await platformCalendarManager.CreateAppointmentCalendarAsync(name)
			.AsTask().ConfigureAwait(false);

		if (color is not null)
		{
			platformCalendar.DisplayColor = AsPlatform(color);
			await platformCalendar.SaveAsync()
				.AsTask().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public async Task DeleteCalendar(string calendarId)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendarManager = await GetAppointmentStore(true)
			.ConfigureAwait(false);

		var calendarToDelete =
			await platformCalendarManager.GetAppointmentCalendarAsync(calendarId)
			.AsTask().ConfigureAwait(false);

		await calendarToDelete.DeleteAsync()
			.AsTask().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null,
		DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

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

		var instance = await GetAppointmentStore().ConfigureAwait(false);

		var events = await instance.FindAppointmentsAsync(sDate,
			eDate.Subtract(sDate), options).AsTask().ConfigureAwait(false);

		// confirm the calendar exists if no events were found
		if ((events is null || events.Count == 0) &&
			!string.IsNullOrEmpty(calendarId))
		{
			await GetCalendar(calendarId).ConfigureAwait(false);
		}

		return ToEvents(events.OrderBy(e => e.StartTime)).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var e = await GetPlatformEvent(eventId);

		return e is null ? throw CalendarStore.InvalidEvent(eventId)
			: ToEvent(e);
	}

	/// <inheritdoc/>
	public Task CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location,
			startDate, endDate, true);
	}

	/// <inheritdoc/>
	public async Task CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay = false)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendarManager = await GetAppointmentStore(true)
			.ConfigureAwait(false);

		var platformCalendar = await platformCalendarManager
			.GetAppointmentCalendarAsync(calendarId)
			.AsTask().ConfigureAwait(false);

		var eventToSave = new Appointment
		{
			Subject = title,
			Details = description,
			Location = location,
			StartTime = startDateTime.LocalDateTime,
			Duration = endDateTime.Subtract(startDateTime),
			AllDay = isAllDay,
		};

		await platformCalendar.SaveAppointmentAsync(eventToSave)
			.AsTask().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task CreateEvent(CalendarEvent calendarEvent)
	{
		return CreateEvent(calendarEvent.CalendarId, calendarEvent.Title, calendarEvent.Description,
			calendarEvent.Location, calendarEvent.StartDate, calendarEvent.EndDate,
			calendarEvent.AllDay);
	}

	/// <inheritdoc/>
	public async Task RemoveEvent(string eventId)
	{
		await EnsureWriteCalendarPermission();

		var e = await GetPlatformEvent(eventId)
			?? throw CalendarStore.InvalidEvent(eventId);
		
		var platformCalendarManager = await GetAppointmentStore(true);

		var calendar = await platformCalendarManager
			.GetAppointmentCalendarAsync(e.CalendarId)
			.AsTask().ConfigureAwait(false)
			?? throw CalendarStore.InvalidCalendar(e.CalendarId);

		await calendar.DeleteAppointmentAsync(e.LocalId)
			.AsTask().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task RemoveEvent(CalendarEvent @event) =>
		RemoveEvent(@event.Id);

	static async Task EnsureWriteCalendarPermission()
	{
		var permissionResult = 
			await Permissions.RequestAsync<Permissions.CalendarWrite>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException(
				"Permission for writing to calendar store is not granted.");
		}
	}

	async Task<AppointmentCalendar?> GetPlatformCalendar(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		var instance = await GetAppointmentStore().ConfigureAwait(false);

		var calendar = await instance.GetAppointmentCalendarAsync(calendarId)
			.AsTask().ConfigureAwait(false);

		return calendar;
	}

	async Task<Appointment?> GetPlatformEvent(string eventId)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		var instance = await GetAppointmentStore().ConfigureAwait(false);

		var e = await instance.GetAppointmentAsync(eventId)
			.AsTask().ConfigureAwait(false);

		return e;
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<AppointmentCalendar> native)
	{
		foreach (var calendar in native)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(AppointmentCalendar calendar) =>
		new(calendar.LocalId, calendar.DisplayName, AsColor(calendar.DisplayColor),
			calendar.CanCreateOrUpdateAppointments);

	// For some reason can't find the .NET MAUI built-in one?
	static Color AsColor(Windows.UI.Color platformColor) => Color.FromRgba(platformColor.R, platformColor.G, platformColor.B, platformColor.A);

	static Windows.UI.Color AsPlatform(Color virtualColor)
	{
		virtualColor.ToRgba(out byte r, out byte g, out byte b, out byte a);

		return Windows.UI.Color.FromArgb(a, r, g, b);
	}

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