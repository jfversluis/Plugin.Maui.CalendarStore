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
	public async Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false, Reminder[]? reminders = null)
	{
		await EnsureWriteCalendarPermission();

		var platformCalendar = EventStore.GetCalendar(calendarId)
			?? throw InvalidCalendar(calendarId);

		if (!platformCalendar.AllowsContentModifications)
		{
			throw new CalendarStoreException($"Selected calendar (id: {calendarId}) is read-only.");
		}

		var eventToSave = EKEvent.FromStore(EventStore);
		eventToSave.Calendar = platformCalendar;
		eventToSave.Title = title;
		eventToSave.Notes = description;
		eventToSave.Location = location;
		eventToSave.StartDate = (NSDate)startDateTime.LocalDateTime;
		eventToSave.EndDate = (NSDate)endDateTime.LocalDateTime;
		eventToSave.AllDay = isAllDay;

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
	public Task<string> CreateEvent(CalendarEvent calendarEvent)
	{
		return CreateEvent(calendarEvent.CalendarId, calendarEvent.Title, calendarEvent.Description,
			calendarEvent.Location, calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.IsAllDay,
			calendarEvent.Reminders.ToArray());
	}

	/// <inheritdoc/>
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location, startDate, endDate, true);
	}

	/// <inheritdoc/>
	public async Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders = null)
	{
		await EnsureWriteCalendarPermission();

		var eventToUpdate = await GetPlatformEvent(eventId);

		eventToUpdate.Title = title;
		eventToUpdate.Notes = description;
		eventToUpdate.Location = location;
		eventToUpdate.StartDate = (NSDate)startDateTime.LocalDateTime;
		eventToUpdate.EndDate = (NSDate)endDateTime.LocalDateTime;
		eventToUpdate.AllDay = isAllDay;

		// Always clear all alarms, because we're going to replace them or remove them
		if (eventToUpdate.Alarms is not null && eventToUpdate.HasAlarms)
		{
			foreach (var alarm in eventToUpdate.Alarms)
			{
				eventToUpdate.RemoveAlarm(alarm);
			}
		}

		// If new reminders were provided, populate alarms again
		if (reminders is not null)
		{
			foreach (var reminder in reminders)
			{
				eventToUpdate.AddAlarm(ToAlarm(reminder));
			}
		}

		var updateResult = EventStore.SaveEvent(eventToUpdate, EKSpan.ThisEvent,
			true, out var error);

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
	public Task UpdateEvent(CalendarEvent eventToUpdate) =>
		UpdateEvent(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate, eventToUpdate.IsAllDay,
			eventToUpdate.Reminders.ToArray());

	/// <inheritdoc/>
	public async Task DeleteEvent(string eventId)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventId);

		await EnsureWriteCalendarPermission();

		var platformEvent = await GetPlatformEvent(eventId);

		var removeResult = EventStore.RemoveEvent(platformEvent, EKSpan.ThisEvent,
			true, out var error);

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

	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete) =>
		DeleteEvent(eventToDelete.Id);

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
		new(calendar.CalendarIdentifier, calendar.Title,
			new UIColor(calendar.CGColor).AsColor(),
			!calendar.AllowsContentModifications);

	static IEnumerable<CalendarEvent> ToEvents(IEnumerable<EKEvent> native)
	{
		foreach (var e in native)
		{
			yield return ToEvent(e);
		}
	}

	static CalendarEvent ToEvent(EKEvent platform) =>
		new(platform.CalendarItemIdentifier,
			platform.Calendar?.CalendarIdentifier ?? string.Empty,
			platform.Title ?? string.Empty)
		{
			Description = platform.Notes ?? string.Empty,
			Location = platform.Location ?? string.Empty,
			IsAllDay = platform.AllDay,
			StartDate = ToDateTimeOffsetWithTimezone(platform.StartDate, platform.TimeZone),
			EndDate = ToDateTimeOffsetWithTimezone(platform.EndDate, platform.TimeZone),
			Attendees = platform.Attendees != null
				? ToAttendees(platform.Attendees).ToList()
				: new List<CalendarEventAttendee>()
		};

	static IEnumerable<CalendarEventAttendee> ToAttendees(IEnumerable<EKParticipant> inviteList)
	{
		foreach (var attendee in inviteList)
		{
			// There is no obvious way to get the attendees email address on iOS?
			yield return new(attendee.Name ?? string.Empty,
				attendee.Name ?? string.Empty);
		}
	}

	static DateTimeOffset ToDateTimeOffsetWithTimezone(NSDate platformDate, NSTimeZone? timezone)
	{
		var timezoneToApply = NSTimeZone.DefaultTimeZone;

		if (timezone is not null)
		{
			timezoneToApply = timezone;
		}

		return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
			(DateTime)platformDate, timezoneToApply.Name);
	}

	static EKAlarm ToAlarm(Reminder reminder)
	{
		return EKAlarm.FromDate((NSDate)reminder.DateTime.LocalDateTime);
	}
}