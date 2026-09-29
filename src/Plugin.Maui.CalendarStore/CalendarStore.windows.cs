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
	public Task DeleteCalendar(Calendar calendarToDelete) =>
		DeleteCalendar(calendarToDelete.Id);

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
		options.FetchProperties.Add(AppointmentProperties.Recurrence);
		options.FetchProperties.Add(AppointmentProperties.OriginalStartTime);

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
	public Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay = false, Reminder[]? reminders = null) =>
		CreateEventCore(calendarId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, null);

	/// <inheritdoc/>
	public Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay, Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId = null) =>
		CreateEventCore(calendarId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, recurrence, timeZoneId);

	/// <inheritdoc/>
	public Task<string> CreateEvent(CalendarEvent calendarEvent) =>
		CreateEventCore(calendarEvent.CalendarId, calendarEvent.Title, calendarEvent.Description,
			calendarEvent.Location, calendarEvent.StartDate, calendarEvent.EndDate,
			calendarEvent.IsAllDay, calendarEvent.Reminders.ToArray(),
			calendarEvent.Recurrence, calendarEvent.TimeZoneId);

	async Task<string> CreateEventCore(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay, Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendar = await GetPlatformCalendar(calendarId, true)
			.ConfigureAwait(false);

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		(startDateTime, endDateTime) = CalendarStore.ResolveStoredTimes(
			startDateTime, endDateTime, isAllDay, timeZone);

		var eventToSave = new Appointment
		{
			Subject = title,
			Details = description,
			Location = location,
			StartTime = startDateTime,
			Duration = CalendarStore.ComputeDuration(startDateTime, endDateTime, isAllDay,
				useWallClockTime: recurrence is not null),
			AllDay = isAllDay,
		};

		if (recurrence is not null)
		{
			eventToSave.Recurrence = ToAppointmentRecurrence(recurrence, timeZone?.Id, startDateTime);
		}

		if (reminders is not null)
		{
			// Windows supports only one reminder, take the first one provided.
			eventToSave.Reminder = eventToSave.StartTime.Subtract(
				reminders[0].DateTime.LocalDateTime);
		}

		await platformCalendar.SaveAppointmentAsync(eventToSave)
			.AsTask().ConfigureAwait(false);

		return eventToSave.LocalId;
	}

	/// <inheritdoc/>
	public Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders = null) =>
		UpdateEventCore(eventId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, null, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate) =>
		UpdateEventCore(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate, eventToUpdate.IsAllDay,
			eventToUpdate.Reminders.ToArray(), eventToUpdate.Recurrence, eventToUpdate.TimeZoneId,
			RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, RecurrenceScope scope, DateTimeOffset? originalOccurrenceStart = null) =>
		UpdateEventCore(eventId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, null, scope, originalOccurrenceStart);

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate, RecurrenceScope scope) =>
		UpdateEventCore(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate, eventToUpdate.IsAllDay,
			eventToUpdate.Reminders.ToArray(), eventToUpdate.Recurrence, eventToUpdate.TimeZoneId,
			scope, eventToUpdate.OriginalOccurrenceStart);

	async Task UpdateEventCore(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId,
		RecurrenceScope scope, DateTimeOffset? originalOccurrenceStart)
	{
		await EnsureWriteCalendarPermission();

		var (platformCalendar, eventToUpdate) = scope == RecurrenceScope.AllEvents
			? await GetMasterForEdit(eventId).ConfigureAwait(false)
			: await GetInstanceForEdit(eventId, originalOccurrenceStart).ConfigureAwait(false);

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		(startDateTime, endDateTime) = CalendarStore.ResolveStoredTimes(
			startDateTime, endDateTime, isAllDay, timeZone);

		var isRecurring = recurrence is not null || eventToUpdate.Recurrence is not null;

		eventToUpdate.Subject = title;
		eventToUpdate.Details = description;
		eventToUpdate.Location = location;
		eventToUpdate.StartTime = startDateTime;
		eventToUpdate.Duration = CalendarStore.ComputeDuration(startDateTime, endDateTime, isAllDay,
			useWallClockTime: isRecurring);
		eventToUpdate.AllDay = isAllDay;

		if (scope == RecurrenceScope.AllEvents && recurrence is not null)
		{
			eventToUpdate.Recurrence = ToAppointmentRecurrence(recurrence, timeZone?.Id, startDateTime);
		}

		if (reminders is not null)
		{
			// Windows supports only one reminder, take the first one provided.
			eventToUpdate.Reminder = eventToUpdate.StartTime.Subtract(
				reminders[0].DateTime.LocalDateTime);
		}
		else
		{
			eventToUpdate.Reminder = null;
		}

		await platformCalendar.SaveAppointmentAsync(eventToUpdate)
			.AsTask().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task DeleteEvent(string eventId) =>
		DeleteEventCore(eventId, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete) =>
		DeleteEventCore(eventToDelete.Id, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task DeleteEvent(string eventId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart = null) =>
		DeleteEventCore(eventId, scope, originalOccurrenceStart);

	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete, RecurrenceScope scope) =>
		DeleteEventCore(eventToDelete.Id, scope, eventToDelete.OriginalOccurrenceStart);

	async Task DeleteEventCore(string eventId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart)
	{
		await EnsureWriteCalendarPermission();

		var e = await GetPlatformEvent(eventId).ConfigureAwait(false);
		var platformCalendar = await GetPlatformCalendar(e.CalendarId, true).ConfigureAwait(false);

		if (scope == RecurrenceScope.ThisEvent)
		{
			var occurrenceStart = originalOccurrenceStart ?? throw new ArgumentException(
				"The original occurrence start is required to target a single occurrence.",
				nameof(originalOccurrenceStart));

			await platformCalendar.DeleteAppointmentInstanceAsync(eventId, occurrenceStart)
				.AsTask().ConfigureAwait(false);
			return;
		}

		await platformCalendar.DeleteAppointmentAsync(e.LocalId)
			.AsTask().ConfigureAwait(false);
	}

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

	async Task<(AppointmentCalendar Calendar, Appointment Event)> GetMasterForEdit(string eventId)
	{
		var eventToEdit = await GetPlatformEvent(eventId).ConfigureAwait(false);
		var calendar = await GetPlatformCalendar(eventToEdit.CalendarId, true).ConfigureAwait(false);

		return (calendar, eventToEdit);
	}

	async Task<(AppointmentCalendar Calendar, Appointment Event)> GetInstanceForEdit(
		string eventId, DateTimeOffset? originalOccurrenceStart)
	{
		var occurrenceStart = originalOccurrenceStart ?? throw new ArgumentException(
			"The original occurrence start is required to target a single occurrence.",
			nameof(originalOccurrenceStart));

		var master = await GetPlatformEvent(eventId).ConfigureAwait(false);
		var calendar = await GetPlatformCalendar(master.CalendarId, true).ConfigureAwait(false);

		var instance = await calendar.GetAppointmentInstanceAsync(eventId, occurrenceStart)
			.AsTask().ConfigureAwait(false);

		return (calendar, instance ?? throw CalendarStore.InvalidEvent(eventId));
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<AppointmentCalendar> platformCalendar)
	{
		foreach (var calendar in platformCalendar)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(AppointmentCalendar calendar) =>
		new(calendar.LocalId, calendar.SourceDisplayName, calendar.DisplayName, AsColor(calendar.DisplayColor),
			!calendar.CanCreateOrUpdateAppointments);

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

	static CalendarEvent ToEvent(Appointment e)
	{
		var timeZoneId = e.Recurrence?.TimeZone;

		return new(e.LocalId, e.CalendarId, e.Subject)
		{
			Description = e.Details,
			Location = e.Location,
			StartDate = e.StartTime,
			IsAllDay = e.AllDay,
			EndDate = e.StartTime.Add(e.Duration),
			Recurrence = e.Recurrence is null ? null : ToCalendarRecurrence(e.Recurrence),
			TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? null : ToIanaTimeZoneId(timeZoneId),
			IsDetached = e.Recurrence?.RecurrenceType == RecurrenceType.ExceptionInstance,
			OriginalOccurrenceStart = e.OriginalStartTime,
			Attendees = e.Invitees != null
				? ToAttendees(e.Invitees).ToList()
				: new List<CalendarEventAttendee>()
		};
	}

	static CalendarRecurrence ToCalendarRecurrence(AppointmentRecurrence recurrence)
	{
		var result = new CalendarRecurrence
		{
			Interval = (int)Math.Max(1, recurrence.Interval),
			Count = recurrence.Occurrences is uint occurrences && occurrences > 0
				? (int)occurrences
				: null,
			Until = recurrence.Until,
		};

		switch (recurrence.Unit)
		{
			case AppointmentRecurrenceUnit.Daily:
				result.Frequency = RecurrenceFrequency.Daily;
				break;
			case AppointmentRecurrenceUnit.Weekly:
				result.Frequency = RecurrenceFrequency.Weekly;
				AddDaysOfWeek(result, recurrence.DaysOfWeek);
				break;
			case AppointmentRecurrenceUnit.Monthly:
				result.Frequency = RecurrenceFrequency.Monthly;
				if (recurrence.Day > 0)
				{
					result.DaysOfMonth.Add((int)recurrence.Day);
				}
				break;
			case AppointmentRecurrenceUnit.MonthlyOnDay:
				result.Frequency = RecurrenceFrequency.Monthly;
				AddOrdinalDay(result, recurrence.DaysOfWeek, FromWeekOfMonth(recurrence.WeekOfMonth));
				break;
			case AppointmentRecurrenceUnit.Yearly:
				result.Frequency = RecurrenceFrequency.Yearly;
				if (recurrence.Month > 0)
				{
					result.MonthsOfYear.Add((int)recurrence.Month);
				}
				if (recurrence.Day > 0)
				{
					result.DaysOfMonth.Add((int)recurrence.Day);
				}
				break;
			case AppointmentRecurrenceUnit.YearlyOnDay:
				result.Frequency = RecurrenceFrequency.Yearly;
				if (recurrence.Month > 0)
				{
					result.MonthsOfYear.Add((int)recurrence.Month);
				}
				AddOrdinalDay(result, recurrence.DaysOfWeek, FromWeekOfMonth(recurrence.WeekOfMonth));
				break;
		}

		return result;
	}

	static AppointmentRecurrence ToAppointmentRecurrence(CalendarRecurrence recurrence,
		string? timeZoneId, DateTimeOffset start)
	{
		RecurrenceRuleParser.Validate(recurrence);

		if (recurrence.DaysOfMonth.Any(day => day < 0))
		{
			throw new NotSupportedException(
				"Windows does not support negative days of the month in recurrence rules.");
		}

		var result = new AppointmentRecurrence
		{
			Interval = (uint)Math.Max(1, recurrence.Interval),
			Occurrences = recurrence.Count is int count ? (uint)count : null,
			Until = recurrence.Until,
		};

		switch (recurrence.Frequency)
		{
			case RecurrenceFrequency.Daily:
				result.Unit = AppointmentRecurrenceUnit.Daily;
				break;
			case RecurrenceFrequency.Weekly:
				result.Unit = AppointmentRecurrenceUnit.Weekly;
				result.DaysOfWeek = ToAppointmentDaysOfWeek(recurrence.DaysOfWeek);
				if (result.DaysOfWeek == AppointmentDaysOfWeek.None)
				{
					result.DaysOfWeek = ToAppointmentDayOfWeek(start.DayOfWeek);
				}
				break;
			case RecurrenceFrequency.Monthly:
				if (TryGetOrdinalDay(recurrence.DaysOfWeek, out var monthlyDay))
				{
					result.Unit = AppointmentRecurrenceUnit.MonthlyOnDay;
					result.WeekOfMonth = ToWeekOfMonth(monthlyDay!.WeekNumber);
					result.DaysOfWeek = ToAppointmentDayOfWeek(monthlyDay.Day);
				}
				else
				{
					result.Unit = AppointmentRecurrenceUnit.Monthly;
					if (recurrence.DaysOfMonth.Count > 0)
					{
						result.Day = (uint)recurrence.DaysOfMonth[0];
					}
					else
					{
						result.Day = (uint)start.Day;
					}
				}
				break;
			case RecurrenceFrequency.Yearly:
				if (recurrence.MonthsOfYear.Count > 0)
				{
					result.Month = (uint)recurrence.MonthsOfYear[0];
				}
				else
				{
					result.Month = (uint)start.Month;
				}

				if (TryGetOrdinalDay(recurrence.DaysOfWeek, out var yearlyDay))
				{
					result.Unit = AppointmentRecurrenceUnit.YearlyOnDay;
					result.WeekOfMonth = ToWeekOfMonth(yearlyDay!.WeekNumber);
					result.DaysOfWeek = ToAppointmentDayOfWeek(yearlyDay.Day);
				}
				else
				{
					result.Unit = AppointmentRecurrenceUnit.Yearly;
					if (recurrence.DaysOfMonth.Count > 0)
					{
						result.Day = (uint)recurrence.DaysOfMonth[0];
					}
					else
					{
						result.Day = (uint)start.Day;
					}
				}
				break;
		}

		// Set the time zone last: assigning other recurrence properties can reset it.
		if (!string.IsNullOrWhiteSpace(timeZoneId))
		{
			result.TimeZone = timeZoneId;
		}

		return result;
	}

	static bool TryGetOrdinalDay(IList<RecurrenceDayOfWeek> daysOfWeek, out RecurrenceDayOfWeek? day)
	{
		day = daysOfWeek.FirstOrDefault(d => d.WeekNumber is not null);
		return day is not null;
	}

	static AppointmentDaysOfWeek ToAppointmentDaysOfWeek(IEnumerable<RecurrenceDayOfWeek> daysOfWeek)
	{
		var result = AppointmentDaysOfWeek.None;

		foreach (var day in daysOfWeek)
		{
			result |= ToAppointmentDayOfWeek(day.Day);
		}

		return result;
	}

	static AppointmentDaysOfWeek ToAppointmentDayOfWeek(DayOfWeek day) =>
		day switch
		{
			DayOfWeek.Sunday => AppointmentDaysOfWeek.Sunday,
			DayOfWeek.Monday => AppointmentDaysOfWeek.Monday,
			DayOfWeek.Tuesday => AppointmentDaysOfWeek.Tuesday,
			DayOfWeek.Wednesday => AppointmentDaysOfWeek.Wednesday,
			DayOfWeek.Thursday => AppointmentDaysOfWeek.Thursday,
			DayOfWeek.Friday => AppointmentDaysOfWeek.Friday,
			DayOfWeek.Saturday => AppointmentDaysOfWeek.Saturday,
			_ => AppointmentDaysOfWeek.None,
		};

	static readonly AppointmentDaysOfWeek[] appointmentDaysOfWeek =
	[
		AppointmentDaysOfWeek.Sunday,
		AppointmentDaysOfWeek.Monday,
		AppointmentDaysOfWeek.Tuesday,
		AppointmentDaysOfWeek.Wednesday,
		AppointmentDaysOfWeek.Thursday,
		AppointmentDaysOfWeek.Friday,
		AppointmentDaysOfWeek.Saturday,
	];

	static void AddDaysOfWeek(CalendarRecurrence recurrence, AppointmentDaysOfWeek daysOfWeek)
	{
		foreach (var day in appointmentDaysOfWeek)
		{
			if (daysOfWeek.HasFlag(day))
			{
				recurrence.DaysOfWeek.Add(new(ToDayOfWeek(day)));
			}
		}
	}

	static void AddOrdinalDay(CalendarRecurrence recurrence, AppointmentDaysOfWeek daysOfWeek,
		int weekNumber)
	{
		foreach (var day in appointmentDaysOfWeek)
		{
			if (daysOfWeek.HasFlag(day))
			{
				recurrence.DaysOfWeek.Add(new(ToDayOfWeek(day), weekNumber));
				return;
			}
		}
	}

	static DayOfWeek ToDayOfWeek(AppointmentDaysOfWeek day) =>
		day switch
		{
			AppointmentDaysOfWeek.Sunday => DayOfWeek.Sunday,
			AppointmentDaysOfWeek.Monday => DayOfWeek.Monday,
			AppointmentDaysOfWeek.Tuesday => DayOfWeek.Tuesday,
			AppointmentDaysOfWeek.Wednesday => DayOfWeek.Wednesday,
			AppointmentDaysOfWeek.Thursday => DayOfWeek.Thursday,
			AppointmentDaysOfWeek.Friday => DayOfWeek.Friday,
			_ => DayOfWeek.Saturday,
		};

	static AppointmentWeekOfMonth ToWeekOfMonth(int? weekNumber) =>
		weekNumber switch
		{
			1 => AppointmentWeekOfMonth.First,
			2 => AppointmentWeekOfMonth.Second,
			3 => AppointmentWeekOfMonth.Third,
			4 => AppointmentWeekOfMonth.Fourth,
			_ => AppointmentWeekOfMonth.Last,
		};

	static int FromWeekOfMonth(AppointmentWeekOfMonth weekOfMonth) =>
		weekOfMonth switch
		{
			AppointmentWeekOfMonth.First => 1,
			AppointmentWeekOfMonth.Second => 2,
			AppointmentWeekOfMonth.Third => 3,
			AppointmentWeekOfMonth.Fourth => 4,
			_ => -1,
		};

	static string ToIanaTimeZoneId(string timeZoneId) =>
		TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId)
			? ianaId
			: timeZoneId;

	static IEnumerable<CalendarEventAttendee> ToAttendees(
		IEnumerable<AppointmentInvitee> platformAttendees)
	{
		foreach (var attendee in platformAttendees)
		{
			yield return new(attendee.DisplayName, attendee.Address);
		}
	}
}
