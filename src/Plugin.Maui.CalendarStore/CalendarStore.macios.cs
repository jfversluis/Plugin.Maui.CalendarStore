using EventKit;
using Foundation;
using Microsoft.Maui.Graphics.Platform;
using UIKit;
using static Plugin.Maui.CalendarStore.CalendarStore;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	static EKEventStore? eventStore;

	static EKEventStore EventStore =>
		eventStore ??= new EKEventStore();

	/// <inheritdoc/>
	public async Task<IEnumerable<Calendar>> GetCalendars()
	{
		await Permissions.RequestAsync<FullAccessCalendar>();

		EventStore.Reset();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		return ToCalendars(calendars).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		var calendar = await GetPlatformCalendar(calendarId);

		return calendar is null ? throw InvalidCalendar(calendarId)
			: ToCalendar(calendar);
	}

	/// <inheritdoc/>
	public async Task<string> CreateCalendar(string name, Color? color = null)
	{
		await EnsureWriteCalendarPermission();

		var calendarToCreate = EKCalendar.Create(EKEntityType.Event, EventStore);
		calendarToCreate.Title = name;

		if (color is not null)
		{
			calendarToCreate.CGColor = color.AsCGColor();
		}

		calendarToCreate.Source = GetBestPossibleSource()
			?? throw new CalendarStoreException(
				"No platform EKSource available to save calendar.");

		var saveResult = EventStore.SaveCalendar(calendarToCreate, true, out var error);

		if (!saveResult || error is not null)
		{
			EventStore.Reset();

			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while saving calendar: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Saving the calendar was unsuccessful.");
		}

		return calendarToCreate.CalendarIdentifier;
	}

	/// <inheritdoc/>
	public async Task UpdateCalendar(string calendarId, string newName, Color? newColor = null)
	{
		await EnsureWriteCalendarPermission();

		var calendarToUpdate = await GetPlatformCalendar(calendarId)
			?? throw InvalidCalendar(calendarId);

		calendarToUpdate.Title = newName;

		if (newColor is not null)
		{
			calendarToUpdate.CGColor = newColor.AsCGColor();
		}

		var saveResult = EventStore.SaveCalendar(calendarToUpdate, true, out var error);

		if (!saveResult || error is not null)
		{
			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while updating calendar: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Updating the calendar was unsuccessful.");
		}
	}

	/// <inheritdoc/>
	public async Task DeleteCalendar(string calendarId)
	{
		await EnsureWriteCalendarPermission();

		var calendar = await GetPlatformCalendar(calendarId)
			?? throw InvalidCalendar(calendarId);

		var removeResult = EventStore.RemoveCalendar(calendar, true, out var error);

		if (!removeResult || error is not null)
		{
			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while deleting calendar: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Deleting the calendar was unsuccessful.");
		}
	}

	/// <inheritdoc/>
	public Task DeleteCalendar(Calendar calendarToDelete) =>
		DeleteCalendar(calendarToDelete.Id);

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null,
		DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<FullAccessCalendar>();

		EventStore.Reset();

		var startDateToConvert = startDate ?? DateTimeOffset.Now.Add(
			defaultStartTimeFromNow);

		// NOTE: 4 years is the maximum period that a iOS calendar events can search
		var endDateToConvert = endDate ?? startDateToConvert.Add(
			defaultEndTimeFromStartTime);

		var sDate = NSDate.FromTimeIntervalSince1970(
			TimeSpan.FromMilliseconds(startDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);

		var eDate = NSDate.FromTimeIntervalSince1970(
			TimeSpan.FromMilliseconds(endDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		if (!string.IsNullOrEmpty(calendarId))
		{
			var calendar = calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);

			if (calendar is null)
			{
				throw InvalidCalendar(calendarId);
			}

			calendars = new[] { calendar };
		}

		var query = EventStore.PredicateForEvents(sDate, eDate, calendars);
		var events = EventStore.EventsMatching(query);

		return ToEvents(events).OrderBy(e => e.StartDate).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId) =>
		ToEvent(await GetPlatformEvent(eventId));

	/// <inheritdoc/>
	public Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false, Reminder[]? reminders = null) =>
		CreateEventCore(calendarId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, null);

	/// <inheritdoc/>
	public Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId = null) =>
		CreateEventCore(calendarId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, recurrence, timeZoneId);

	/// <inheritdoc/>
	public Task<string> CreateEvent(CalendarEvent calendarEvent) =>
		CreateEventCore(calendarEvent.CalendarId, calendarEvent.Title, calendarEvent.Description,
			calendarEvent.Location, calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.IsAllDay,
			calendarEvent.Reminders.ToArray(), calendarEvent.Recurrence, calendarEvent.TimeZoneId);

	async Task<string> CreateEventCore(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId)
	{
		if (recurrence is not null && !isAllDay && endDateTime <= startDateTime)
		{
			throw new CalendarStoreException(
				"The end date and time must be after the start date and time for a recurring event.");
		}

		await EnsureWriteCalendarPermission();

		var platformCalendar = EventStore.GetCalendar(calendarId)
			?? throw InvalidCalendar(calendarId);

		if (!platformCalendar.AllowsContentModifications)
		{
			throw new CalendarStoreException($"Selected calendar (id: {calendarId}) is read-only.");
		}

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		var resolvedStart = CalendarStore.ResolveWallTime(startDateTime, timeZone);
		var resolvedEnd = CalendarStore.ResolveWallTime(endDateTime, timeZone);

		var eventToSave = EKEvent.FromStore(EventStore);
		eventToSave.Calendar = platformCalendar;
		eventToSave.Title = title;
		eventToSave.Notes = description;
		eventToSave.Location = location;
		eventToSave.StartDate = ToNSDate(resolvedStart);
		eventToSave.EndDate = ToNSDate(resolvedEnd);
		eventToSave.AllDay = isAllDay;

		if (timeZone is not null)
		{
			eventToSave.TimeZone = NSTimeZone.FromName(timeZone.Id);
		}

		if (recurrence is not null)
		{
			eventToSave.AddRecurrenceRule(ToEKRecurrenceRule(recurrence));
		}

		if (reminders is not null)
		{
			foreach (var reminder in reminders)
			{
				eventToSave.AddAlarm(ToAlarm(reminder));
			}
		}

		var saveResult = EventStore.SaveEvent(eventToSave, EKSpan.ThisEvent, true, out var error);

		if (!saveResult || error is not null)
		{
			EventStore.Reset();

			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while saving event: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Saving the event was unsuccessful.");
		}

		return eventToSave.CalendarItemIdentifier;
	}

	/// <inheritdoc/>
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location, startDate, endDate, true);
	}

	/// <inheritdoc/>
	public Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders = null) =>
		UpdateEventCore(eventId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, RecurrenceScope scope, DateTimeOffset? originalOccurrenceStart = null) =>
		UpdateEventCore(eventId, title, description, location, startDateTime, endDateTime,
			isAllDay, reminders, null, scope, originalOccurrenceStart);

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate) =>
		UpdateEventCore(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate,
			eventToUpdate.IsAllDay, eventToUpdate.Reminders.ToArray(), eventToUpdate.TimeZoneId,
			RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate, RecurrenceScope scope) =>
		UpdateEventCore(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate,
			eventToUpdate.IsAllDay, eventToUpdate.Reminders.ToArray(), eventToUpdate.TimeZoneId,
			scope, eventToUpdate.OriginalOccurrenceStart);

	async Task UpdateEventCore(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders, string? timeZoneId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await EnsureWriteCalendarPermission();

		var eventToUpdate = await GetPlatformEventForScope(eventId, scope, originalOccurrenceStart);

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		var resolvedStart = CalendarStore.ResolveWallTime(startDateTime, timeZone);
		var resolvedEnd = CalendarStore.ResolveWallTime(endDateTime, timeZone);

		eventToUpdate.Title = title;
		eventToUpdate.Notes = description;
		eventToUpdate.Location = location;
		eventToUpdate.StartDate = ToNSDate(resolvedStart);
		eventToUpdate.EndDate = ToNSDate(resolvedEnd);
		eventToUpdate.AllDay = isAllDay;

		if (timeZone is not null)
		{
			eventToUpdate.TimeZone = NSTimeZone.FromName(timeZone.Id);
		}

		// Always clear all alarms, because we're going to replace them or remove them
		if (eventToUpdate.Alarms is not null && eventToUpdate.HasAlarms)
		{
			foreach (var alarm in eventToUpdate.Alarms)
			{
				eventToUpdate.RemoveAlarm(alarm);
			}
		}

		if (reminders is not null)
		{
			foreach (var reminder in reminders)
			{
				eventToUpdate.AddAlarm(ToAlarm(reminder));
			}
		}

		var updateResult = EventStore.SaveEvent(eventToUpdate, ToEKSpan(scope), true, out var error);

		if (!updateResult || error is not null)
		{
			EventStore.Reset();

			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while updating event: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Updating the event was unsuccessful.");
		}
	}

	/// <inheritdoc/>
	public Task DeleteEvent(string eventId) =>
		DeleteEventCore(eventId, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task DeleteEvent(string eventId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart = null) =>
		DeleteEventCore(eventId, scope, originalOccurrenceStart);

	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete) =>
		DeleteEventCore(eventToDelete.Id, RecurrenceScope.AllEvents, null);

	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete, RecurrenceScope scope) =>
		DeleteEventCore(eventToDelete.Id, scope, eventToDelete.OriginalOccurrenceStart);

	async Task DeleteEventCore(string eventId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await EnsureWriteCalendarPermission();

		var platformEvent = await GetPlatformEventForScope(eventId, scope, originalOccurrenceStart);

		var removeResult = EventStore.RemoveEvent(platformEvent, ToEKSpan(scope), true, out var error);

		if (!removeResult || error is not null)
		{
			EventStore.Reset();

			if (error is not null)
			{
				throw new CalendarStoreException($"Error occurred while removing event: " +
					$"{error.LocalizedDescription}");
			}

			throw new CalendarStoreException("Removing the event was unsuccessful.");
		}
	}

	static EKSpan ToEKSpan(RecurrenceScope scope) =>
		scope == RecurrenceScope.ThisEvent ? EKSpan.ThisEvent : EKSpan.FutureEvents;

	static async Task<EKEvent> GetPlatformEventForScope(string eventId, RecurrenceScope scope,
		DateTimeOffset? originalOccurrenceStart)
	{
		if (scope == RecurrenceScope.AllEvents)
		{
			return await GetPlatformEvent(eventId);
		}

		if (originalOccurrenceStart is not DateTimeOffset occurrenceStart)
		{
			throw new ArgumentException(
				"The original occurrence start is required to target a single occurrence.",
				nameof(originalOccurrenceStart));
		}

		return await GetPlatformOccurrence(eventId, occurrenceStart) ?? throw InvalidEvent(eventId);
	}

	static async Task<EKEvent?> GetPlatformOccurrence(string eventId, DateTimeOffset originalOccurrenceStart)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await Permissions.RequestAsync<FullAccessCalendar>();

		EventStore.Reset();

		var targetMillis = originalOccurrenceStart.ToUnixTimeMilliseconds();
		var calendar = (EventStore.GetCalendarItem(eventId) as EKEvent)?.Calendar;
		var calendars = calendar is null ? null : new[] { calendar };

		// Start with a narrow query for the common case, then widen up to EventKit's
		// four-year query limit so occurrences moved away from their original slot
		// can still be found for a subsequent edit or deletion.
		foreach (var halfWindow in new[]
		{
			TimeSpan.FromDays(1),
			TimeSpan.FromDays(31),
			TimeSpan.FromDays(366),
			TimeSpan.FromDays(730),
		})
		{
			var windowMillis = halfWindow.TotalMilliseconds;
			var start = NSDate.FromTimeIntervalSince1970((targetMillis - windowMillis) / 1000d);
			var end = NSDate.FromTimeIntervalSince1970((targetMillis + windowMillis) / 1000d);
			var predicate = EventStore.PredicateForEvents(start, end, calendars);
			var occurrence = EventStore.EventsMatching(predicate)?.FirstOrDefault(e =>
				e.CalendarItemIdentifier == eventId
				&& e.OccurrenceDate is not null
				&& (long)Math.Round(e.OccurrenceDate.SecondsSince1970 * 1000d) == targetMillis);

			if (occurrence is not null)
			{
				return occurrence;
			}
		}

		return null;
	}

	static async Task EnsureWriteCalendarPermission()
	{
		var permissionResult = await Permissions.RequestAsync<WriteOnlyCalendar>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException("Permission for writing to calendar store is not granted.");
		}
	}

	static EKSource? GetBestPossibleSource()
	{
		if (EventStore.DefaultCalendarForNewEvents?.Source is not null)
		{
			return EventStore.DefaultCalendarForNewEvents.Source;
		}

		var remoteSource = EventStore.Sources.Where(
			s => s.SourceType == EKSourceType.CalDav).FirstOrDefault();

		if (remoteSource is not null)
		{
			return remoteSource;
		}

		var localSource = EventStore.Sources.Where(
			s => s.SourceType == EKSourceType.Local).FirstOrDefault();

		if (localSource is not null)
		{
			return localSource;
		}

		return null;
	}

	static async Task<EKCalendar?> GetPlatformCalendar(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<FullAccessCalendar>();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		return calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);
	}

	static async Task<EKEvent> GetPlatformEvent(string eventId)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await Permissions.RequestAsync<FullAccessCalendar>();

		if (EventStore.GetCalendarItem(eventId) is not EKEvent calendarEvent)
		{
			throw InvalidEvent(eventId);
		}

		return calendarEvent;
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<EKCalendar> native)
	{
		foreach (var calendar in native)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(EKCalendar calendar) =>
		new(calendar.CalendarIdentifier, calendar.Source?.Title ?? string.Empty, calendar.Title,
			new UIColor(calendar.CGColor).AsColor(),
			!calendar.AllowsContentModifications);

	static IEnumerable<CalendarEvent> ToEvents(IEnumerable<EKEvent> native)
	{
		foreach (var e in native)
		{
			yield return ToEvent(e);
		}
	}

	static CalendarEvent ToEvent(EKEvent platform)
	{
		var isAllDay = platform.AllDay;
		var timeZone = platform.TimeZone;
		var isDetached = platform.IsDetached;
		var isRecurring = platform.HasRecurrenceRules;

		return new(platform.CalendarItemIdentifier,
			platform.Calendar?.CalendarIdentifier ?? string.Empty,
			platform.Title ?? string.Empty)
		{
			Description = platform.Notes ?? string.Empty,
			Location = platform.Location ?? string.Empty,
			IsAllDay = isAllDay,
			StartDate = ToDateTimeOffsetWithTimezone(platform.StartDate, timeZone),
			EndDate = ToDateTimeOffsetWithTimezone(platform.EndDate, timeZone),
			TimeZoneId = isAllDay ? null : timeZone?.Name,
			Recurrence = isRecurring ? ToRecurrence(platform) : null,
			IsDetached = isDetached,
			OriginalOccurrenceStart = isRecurring || isDetached
				? ToNullableDateTimeOffsetWithTimezone(platform.OccurrenceDate, timeZone)
				: null,
			EventColor = platform.Calendar?.CGColor is not null
				? new UIColor(platform.Calendar.CGColor).AsColor()
				: null,
			Attendees = platform.Attendees != null
				? ToAttendees(platform.Attendees).ToList()
				: new List<CalendarEventAttendee>()
		};
	}

	static CalendarRecurrence? ToRecurrence(EKEvent platform)
	{
		var rule = platform.RecurrenceRules?.FirstOrDefault();

		return rule is null ? null : ToRecurrence(rule);
	}

	static CalendarRecurrence ToRecurrence(EKRecurrenceRule rule)
	{
		var recurrence = new CalendarRecurrence
		{
			Frequency = ToRecurrenceFrequency(rule.Frequency),
			Interval = (int)rule.Interval,
			FirstDayOfWeek = ToNullableDayOfWeek(rule.FirstDayOfTheWeek),
		};

		if (rule.RecurrenceEnd is { } end)
		{
			if (end.EndDate is { } endDate)
			{
				recurrence.Until = DateTimeOffset.FromUnixTimeMilliseconds(
					(long)Math.Round(endDate.SecondsSince1970 * 1000d));
			}
			else if (end.OccurrenceCount > 0)
			{
				recurrence.Count = (int)end.OccurrenceCount;
			}
		}

		if (rule.DaysOfTheWeek is not null)
		{
			foreach (var dayOfWeek in rule.DaysOfTheWeek)
			{
				recurrence.DaysOfWeek.Add(new(
					ToDayOfWeek(dayOfWeek.DayOfTheWeek),
					dayOfWeek.WeekNumber != 0 ? (int)dayOfWeek.WeekNumber : null));
			}
		}

		AddIntegers(rule.DaysOfTheMonth, recurrence.DaysOfMonth);
		AddIntegers(rule.MonthsOfTheYear, recurrence.MonthsOfYear);
		AddIntegers(rule.WeeksOfTheYear, recurrence.WeeksOfYear);
		AddIntegers(rule.DaysOfTheYear, recurrence.DaysOfYear);
		AddIntegers(rule.SetPositions, recurrence.SetPositions);

		return recurrence;
	}

	static void AddIntegers(NSNumber[]? values, IList<int> target)
	{
		if (values is null)
		{
			return;
		}

		foreach (var value in values)
		{
			target.Add(value.Int32Value);
		}
	}

	static RecurrenceFrequency ToRecurrenceFrequency(EKRecurrenceFrequency frequency) =>
		frequency switch
		{
			EKRecurrenceFrequency.Daily => RecurrenceFrequency.Daily,
			EKRecurrenceFrequency.Weekly => RecurrenceFrequency.Weekly,
			EKRecurrenceFrequency.Monthly => RecurrenceFrequency.Monthly,
			EKRecurrenceFrequency.Yearly => RecurrenceFrequency.Yearly,
			_ => throw new CalendarStoreException($"Unsupported recurrence frequency: {frequency}."),
		};

	static DayOfWeek ToDayOfWeek(EKWeekday day) =>
		day switch
		{
			EKWeekday.Sunday => DayOfWeek.Sunday,
			EKWeekday.Monday => DayOfWeek.Monday,
			EKWeekday.Tuesday => DayOfWeek.Tuesday,
			EKWeekday.Wednesday => DayOfWeek.Wednesday,
			EKWeekday.Thursday => DayOfWeek.Thursday,
			EKWeekday.Friday => DayOfWeek.Friday,
			EKWeekday.Saturday => DayOfWeek.Saturday,
			_ => throw new CalendarStoreException($"Unsupported day of the week: {day}."),
		};

	static DayOfWeek? ToNullableDayOfWeek(EKWeekday day) =>
		day == EKWeekday.NotSet ? null : ToDayOfWeek(day);

	static EKRecurrenceRule ToEKRecurrenceRule(CalendarRecurrence recurrence)
	{
		RecurrenceRuleParser.Validate(recurrence);

		var end = recurrence.Count is int count
			? EKRecurrenceEnd.FromOccurrenceCount(count)
			: recurrence.Until is DateTimeOffset until
				? EKRecurrenceEnd.FromEndDate(ToNSDate(until))
				: null;

		var daysOfWeek = recurrence.DaysOfWeek.Count > 0
			? recurrence.DaysOfWeek.Select(day => day.WeekNumber is int weekNumber
				? EKRecurrenceDayOfWeek.FromDay(ToEKWeekday(day.Day), weekNumber)
				: EKRecurrenceDayOfWeek.FromDay(ToEKWeekday(day.Day))).ToArray()
			: null;

		return new EKRecurrenceRule(ToEKRecurrenceFrequency(recurrence.Frequency),
			recurrence.Interval,
			daysOfWeek,
			ToNSNumbers(recurrence.DaysOfMonth),
			ToNSNumbers(recurrence.MonthsOfYear),
			ToNSNumbers(recurrence.WeeksOfYear),
			ToNSNumbers(recurrence.DaysOfYear),
			ToNSNumbers(recurrence.SetPositions),
			end);
	}

	static NSNumber[]? ToNSNumbers(IList<int> values) =>
		values.Count > 0 ? values.Select(NSNumber.FromInt32).ToArray() : null;

	static EKRecurrenceFrequency ToEKRecurrenceFrequency(RecurrenceFrequency frequency) =>
		frequency switch
		{
			RecurrenceFrequency.Daily => EKRecurrenceFrequency.Daily,
			RecurrenceFrequency.Weekly => EKRecurrenceFrequency.Weekly,
			RecurrenceFrequency.Monthly => EKRecurrenceFrequency.Monthly,
			RecurrenceFrequency.Yearly => EKRecurrenceFrequency.Yearly,
			_ => throw new CalendarStoreException($"Unsupported recurrence frequency: {frequency}."),
		};

	static EKWeekday ToEKWeekday(DayOfWeek day) =>
		day switch
		{
			DayOfWeek.Sunday => EKWeekday.Sunday,
			DayOfWeek.Monday => EKWeekday.Monday,
			DayOfWeek.Tuesday => EKWeekday.Tuesday,
			DayOfWeek.Wednesday => EKWeekday.Wednesday,
			DayOfWeek.Thursday => EKWeekday.Thursday,
			DayOfWeek.Friday => EKWeekday.Friday,
			DayOfWeek.Saturday => EKWeekday.Saturday,
			_ => throw new CalendarStoreException($"Unsupported day of the week: {day}."),
		};

	static NSDate ToNSDate(DateTimeOffset value) =>
		NSDate.FromTimeIntervalSince1970(value.ToUnixTimeMilliseconds() / 1000d);

	static IEnumerable<CalendarEventAttendee> ToAttendees(IEnumerable<EKParticipant> inviteList)
	{
		foreach (var attendee in inviteList)
		{
			// There is no obvious way to get the attendees email address on iOS?
			yield return new(attendee.Name ?? string.Empty,
				ToEmailAddress(attendee.Url));
		}
	}
	
	static string ToEmailAddress(NSUrl? url)
	{
		if (url is null)
			return string.Empty;
		
		var urlString = url.AbsoluteString;
		if (string.IsNullOrEmpty(urlString))
			return string.Empty;

		if (Uri.TryCreate(urlString, UriKind.Absolute, out var uri) && uri!.Scheme == Uri.UriSchemeMailto)
			return $"{uri.UserInfo}@{uri.DnsSafeHost}";

		return string.Empty;
	}
	
	static DateTimeOffset ToDateTimeOffsetWithTimezone(NSDate platformDate, NSTimeZone? timezone)
	{
		var timezoneToApply = timezone ?? NSTimeZone.DefaultTimeZone;

		return CalendarStore.FromUnixTimeMilliseconds(
			(long)Math.Round(platformDate.SecondsSince1970 * 1000d),
			CalendarStore.ResolveTimeZone(timezoneToApply?.Name));
	}

	static DateTimeOffset? ToNullableDateTimeOffsetWithTimezone(NSDate? platformDate, NSTimeZone? timezone) =>
		platformDate is null ? null : ToDateTimeOffsetWithTimezone(platformDate, timezone);

	static EKAlarm ToAlarm(Reminder reminder)
	{
		return EKAlarm.FromDate((NSDate)reminder.DateTime.LocalDateTime);
	}
}
