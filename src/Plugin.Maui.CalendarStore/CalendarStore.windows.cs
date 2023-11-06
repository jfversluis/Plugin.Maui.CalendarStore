using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Windows.ApplicationModel.Appointments;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	AppointmentStore? store;
	bool canWriteToStore;

	async Task<AppointmentStore> GetAppointmentStore(bool requestWrite = false)
	{
		if (store is not null &&
			(canWriteToStore || !requestWrite))
		{
			return store;
		}

		store = await AppointmentManager.RequestStoreAsync(
			requestWrite ? AppointmentStoreAccessType.AllCalendarsReadWrite
			: AppointmentStoreAccessType.AllCalendarsReadOnly).AsTask();
		canWriteToStore = requestWrite;
		return store;
	}

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

		return ToCalendar(calendar);
	}

	/// <inheritdoc/>
	public async Task<string> CreateCalendar(string name, Color? color = null)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendarManager = await GetAppointmentStore(true)
			.ConfigureAwait(false);

		var calendarToCreate =
			await platformCalendarManager.CreateAppointmentCalendarAsync(name)
			.AsTask().ConfigureAwait(false);

		if (color is not null)
		{
			calendarToCreate.DisplayColor = AsPlatform(color);

			await calendarToCreate.SaveAsync()
				.AsTask().ConfigureAwait(false);
		}

		return calendarToCreate.LocalId;
	}

	/// <inheritdoc/>
	public async Task UpdateCalendar(string calendarId, string newName, Color? newColor = null)
	{
		await EnsureWriteCalendarPermission();

		var calendarToUpdate = await GetPlatformCalendar(calendarId, true);

		calendarToUpdate.DisplayName = newName;

		if (newColor is not null)
		{
			calendarToUpdate.DisplayColor = AsPlatform(newColor);
		}

		await calendarToUpdate.SaveAsync()
			.AsTask().ConfigureAwait(false);
	}

	///// <inheritdoc/>
	//public async Task DeleteCalendar(string calendarId)
	//{
	//	await EnsureWriteCalendarPermission();

	//	var platformCalendarManager = await GetAppointmentStore(true)
	//		.ConfigureAwait(false);

	//	var calendarToDelete =
	//		await platformCalendarManager.GetAppointmentCalendarAsync(calendarId)
	//		.AsTask().ConfigureAwait(false);

	//	await calendarToDelete.DeleteAsync()
	//		.AsTask().ConfigureAwait(false);
	//}

	///// <inheritdoc/>
	//public Task DeleteCalendar(Calendar calendarToDelete) =>
	//	DeleteCalendar(calendarToDelete.Id);

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

		if (events is null)
		{
			return Array.Empty<CalendarEvent>();
		}

		return ToEvents(events.OrderBy(e => e.StartTime)).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var eventToReturn = await GetPlatformEvent(eventId);

		return ToEvent(eventToReturn);
	}

	/// <inheritdoc/>
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location,
			startDate, endDate, true);
	}

	/// <inheritdoc/>
	public async Task<string> CreateEvent(string calendarId, string title, string description,
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

		return eventToSave.LocalId;
	}

	/// <inheritdoc/>
	public Task<string> CreateEvent(CalendarEvent calendarEvent)
	{
		return CreateEvent(calendarEvent.CalendarId, calendarEvent.Title, calendarEvent.Description,
			calendarEvent.Location, calendarEvent.StartDate, calendarEvent.EndDate,
			calendarEvent.IsAllDay);
	}

	/// <inheritdoc/>
	public async Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay)
	{
		await EnsureWriteCalendarPermission();

		var eventToUpdate = await GetPlatformEvent(eventId);

		var platformCalendar =
			await GetPlatformCalendar(eventToUpdate.CalendarId, true);

		eventToUpdate.Subject = title;
		eventToUpdate.Details = description;
		eventToUpdate.Location = location;
		eventToUpdate.StartTime = startDateTime.LocalDateTime;
		eventToUpdate.Duration = endDateTime.Subtract(startDateTime);
		eventToUpdate.AllDay = isAllDay;

		await platformCalendar.SaveAppointmentAsync(eventToUpdate)
			.AsTask().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate) =>
		UpdateEvent(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate, eventToUpdate.IsAllDay);

	/// <inheritdoc/>
	public async Task DeleteEvent(string eventId)
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
	public Task DeleteEvent(CalendarEvent eventToDelete) =>
		DeleteEvent(eventToDelete.Id);

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

	async Task<AppointmentCalendar> GetPlatformCalendar(string calendarId,
		bool requestWriteAccess = false)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		var instance = await GetAppointmentStore(requestWriteAccess)
			.ConfigureAwait(false);

		var calendar = await instance.GetAppointmentCalendarAsync(calendarId)
			.AsTask().ConfigureAwait(false);

		return calendar ?? throw CalendarStore.InvalidCalendar(calendarId);
	}

	async Task<Appointment> GetPlatformEvent(string eventId)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		var instance = await GetAppointmentStore()
			.ConfigureAwait(false);

		var eventToReturn = await instance.GetAppointmentAsync(eventId)
			.AsTask().ConfigureAwait(false);

		return eventToReturn ?? throw CalendarStore.InvalidEvent(eventId);
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<AppointmentCalendar> platformCalendar)
	{
		foreach (var calendar in platformCalendar)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(AppointmentCalendar calendar) =>
		new(calendar.LocalId, calendar.DisplayName, AsColor(calendar.DisplayColor),
			calendar.CanCreateOrUpdateAppointments);

	// For some reason can't find the .NET MAUI built-in one?
	static Color AsColor(Windows.UI.Color platformColor) =>
		Color.FromRgba(platformColor.R, platformColor.G, platformColor.B, platformColor.A);

	static Windows.UI.Color AsPlatform(Color virtualColor)
	{
		virtualColor.ToRgba(out byte r, out byte g, out byte b, out byte a);

		return Windows.UI.Color.FromArgb(a, r, g, b);
	}

	static IEnumerable<CalendarEvent> ToEvents(IEnumerable<Appointment> platformEvents)
	{
		foreach (var e in platformEvents)
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
			IsAllDay = e.AllDay,
			EndDate = e.StartTime.Add(e.Duration),
			Attendees = e.Invitees != null
				? ToAttendees(e.Invitees).ToList()
				: new List<CalendarEventAttendee>()
		};

	static IEnumerable<CalendarEventAttendee> ToAttendees(
		IEnumerable<AppointmentInvitee> platformAttendees)
	{
		foreach (var attendee in platformAttendees)
		{
			yield return new(attendee.DisplayName, attendee.Address);
		}
	}

	public Task<string> CreateEventWithReminder(string calendarId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, int reminderMinutes, bool isAllDay = false) => throw new NotImplementedException();
	public Task UpdateEventWithReminder(string eventId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, int reminderMinutes) => throw new NotImplementedException();
}