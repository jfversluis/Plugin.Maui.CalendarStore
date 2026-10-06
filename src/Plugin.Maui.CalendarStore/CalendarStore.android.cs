using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using System.Globalization;
using static Plugin.Maui.CalendarStore.CalendarStore;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	const string detachedUidPrefix = "plugin-maui-calendarstore-detached:";

	readonly Color defaultColor = Color.FromRgb(0x51, 0x2B, 0xD4);

	readonly Android.Net.Uri calendarsTableUri;
	readonly Android.Net.Uri eventsTableUri;
	readonly Android.Net.Uri attendeesTableUri;
	readonly Android.Net.Uri remindersTableUri;

	readonly ContentResolver platformContentResolver;

	readonly List<string> calendarColumns =
		[
			CalendarContract.Calendars.InterfaceConsts.Id,
				CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
				CalendarContract.Calendars.InterfaceConsts.CalendarColor,
				CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel,
				CalendarContract.Calendars.InterfaceConsts.AccountName,
			];

	readonly List<string> eventsColumns = [
			CalendarContract.Events.InterfaceConsts.Id,
				CalendarContract.Events.InterfaceConsts.CalendarId,
				CalendarContract.Events.InterfaceConsts.Title,
				CalendarContract.Events.InterfaceConsts.Description,
				CalendarContract.Events.InterfaceConsts.EventLocation,
				CalendarContract.Events.InterfaceConsts.AllDay,
				CalendarContract.Events.InterfaceConsts.Dtstart,
				CalendarContract.Events.InterfaceConsts.Dtend,
				CalendarContract.Events.InterfaceConsts.Duration,
				CalendarContract.Events.InterfaceConsts.Rrule,
				CalendarContract.Events.InterfaceConsts.OriginalId,
				CalendarContract.Events.InterfaceConsts.OriginalInstanceTime,
				CalendarContract.Events.InterfaceConsts.Deleted,
				CalendarContract.Events.InterfaceConsts.EventTimezone,
				CalendarContract.Events.InterfaceConsts.EventColor,
			];

	readonly List<string> instancesColumns = [
			CalendarContract.Instances.EventId,
				CalendarContract.Events.InterfaceConsts.CalendarId,
				CalendarContract.Events.InterfaceConsts.Title,
				CalendarContract.Events.InterfaceConsts.Description,
				CalendarContract.Events.InterfaceConsts.EventLocation,
				CalendarContract.Events.InterfaceConsts.AllDay,
				CalendarContract.Instances.Begin,
				CalendarContract.Instances.End,
				CalendarContract.Events.InterfaceConsts.EventTimezone,
				CalendarContract.Events.InterfaceConsts.Rrule,
				CalendarContract.Events.InterfaceConsts.OriginalId,
				CalendarContract.Events.InterfaceConsts.OriginalInstanceTime,
				CalendarContract.Events.InterfaceConsts.EventColor,
			];

	readonly List<string> attendeesColumns =
		[
			CalendarContract.Attendees.InterfaceConsts.EventId,
				CalendarContract.Attendees.InterfaceConsts.AttendeeEmail,
				CalendarContract.Attendees.InterfaceConsts.AttendeeName,
			];

	public CalendarStoreImplementation()
	{
		calendarsTableUri = CalendarContract.Calendars.ContentUri
			?? throw new CalendarStoreException(
				"Could not determine Android calendars table URI.");

		eventsTableUri = CalendarContract.Events.ContentUri
			?? throw new CalendarStoreException(
				"Could not determine Android events table URI.");

		attendeesTableUri = CalendarContract.Attendees.ContentUri
			?? throw new CalendarStoreException(
				"Could not determine Android attendees table URI.");

		remindersTableUri = CalendarContract.Reminders.ContentUri
			?? throw new CalendarStoreException(
				"Could not determine Android reminders table URI.");

		platformContentResolver = Platform.AppContext.ApplicationContext?.ContentResolver
			?? throw new CalendarStoreException(
				"Could not determine Android events table URI.");
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<Calendar>> GetCalendars()
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var queryConditions =
			$"{CalendarContract.Calendars.InterfaceConsts.Deleted} != 1";

		using var cursor = platformContentResolver?.Query(calendarsTableUri,
			calendarColumns.ToArray(), queryConditions, null, null)
			?? throw new CalendarStoreException("Error while querying calendars");

		return ToCalendars(cursor, calendarColumns).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		using var cursor = await GetPlatformCalendar(calendarId);

		var calendar = ToCalendar(cursor, calendarColumns);

		SafeCloseCursor(cursor);

		return calendar;
	}

	/// <inheritdoc/>
	public async Task<string> CreateCalendar(string name, Color? color = null)
	{
		await EnsureWriteCalendarPermission();

		ContentValues calendarToCreate = new();

		// Mandatory fields when inserting a calendar.
		// See https://developer.android.com/reference/android/provider/CalendarContract.Calendars#operations.
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.AccountName, name);
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.AccountType, CalendarContract.AccountTypeLocal);
		calendarToCreate.Put(CalendarContract.Calendars.Name, name);
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName, name);
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.CalendarColor, (color ?? defaultColor).AsColor());
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel, (int)CalendarAccess.AccessOwner);
		calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.OwnerAccount, name);

		// Inserting new calendars should be done as a sync adapter.
		var insertCalendarUri = calendarsTableUri.BuildUpon()
		?.AppendQueryParameter(CalendarContract.CallerIsSyncadapter, "true")
		?.AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountName, name)
		?.AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountType, CalendarContract.AccountTypeLocal)
		?.Build()
		?? throw new CalendarStoreException("There was an error saving the calendar.");

		var idUrl = platformContentResolver?.Insert(insertCalendarUri, calendarToCreate);

		if (!long.TryParse(idUrl?.LastPathSegment, out var savedId))
		{
			throw new CalendarStoreException("There was an error saving the calendar.");
		}

		return savedId.ToString();
	}

	/// <inheritdoc/>
	public async Task UpdateCalendar(string calendarId, string newName, Color? newColor = null)
	{
		await EnsureWriteCalendarPermission();

		ContentValues calendarToUpdate = new();

		// We just want to know a calendar with this ID exists,
		// but we also need to dispose the returned cursor.
		using var cursor = await GetPlatformCalendar(calendarId);

		calendarToUpdate.Put(CalendarContract.Calendars.InterfaceConsts.Id, calendarId);

		calendarToUpdate.Put(
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
			newName);

		if (newColor is not null)
		{
			calendarToUpdate.Put(
				CalendarContract.Calendars.InterfaceConsts.CalendarColor,
				newColor.AsColor());
		}

		var calendarToUpdateUri =
			ContentUris.WithAppendedId(calendarsTableUri, long.Parse(calendarId));

		var updateCount = platformContentResolver?.Update(calendarToUpdateUri,
			calendarToUpdate, null, null);

		SafeCloseCursor(cursor);

		if (updateCount != 1)
		{
			throw new CalendarStoreException(
				"There was an error updating the calendar.");
		}
	}

	/// <inheritdoc/>
	public async Task DeleteCalendar(string calendarId)
	{
		await EnsureWriteCalendarPermission();

		// Android ids are always integers
		if (string.IsNullOrEmpty(calendarId) ||
			!long.TryParse(calendarId, out long platformCalendarId))
		{
			throw InvalidCalendar(calendarId);
		}

		// We just want to know a calendar with this ID exists,
		// but we also need to dispose the returned cursor.
		using var cursor = await GetPlatformCalendar(calendarId);

		var deleteEventUri = ContentUris.WithAppendedId(calendarsTableUri, platformCalendarId);
		var deleteCount = platformContentResolver?.Delete(deleteEventUri, null, null);

		SafeCloseCursor(cursor);

		if (deleteCount != 1)
		{
			throw new CalendarStoreException(
				"There was an error deleting the calendar.");
		}
	}

	/// <inheritdoc/>
	public Task DeleteCalendar(Calendar calendarToDelete) =>
		DeleteCalendar(calendarToDelete.Id);

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(
		string? calendarId = null, DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(calendarId) && !int.TryParse(calendarId, out _))
		{
			throw InvalidCalendar(calendarId);
		}

		var sDate = startDate ?? DateTimeOffset.Now.Add(defaultStartTimeFromNow);
		var eDate = endDate ?? sDate.Add(defaultEndTimeFromStartTime);

		var startMillis = CalendarStore.ToFilterTimestampMillis(sDate);
		var endMillis = CalendarStore.ToFilterTimestampMillis(eDate);

		// Build the Instances URI with date range encoded in the path.
		// This is the Android-recommended way to query events by date range,
		// as the Instances table expands recurring events and reflects
		// externally synced changes (fixes stale data from issue #51).
		var builder = CalendarContract.Instances.ContentUri!.BuildUpon()!;
		ContentUris.AppendId(builder, startMillis);
		ContentUris.AppendId(builder, endMillis);
		var instancesUri = builder.Build()
			?? throw new CalendarStoreException("Could not build Instances query URI.");

		var selection = CalendarStore.BuildInstancesSelection(
			CalendarContract.Events.InterfaceConsts.Deleted,
			CalendarContract.Events.InterfaceConsts.CalendarId,
			calendarId);

		var sortOrder = $"{CalendarContract.Instances.Begin} ASC";

		using var cursor = platformContentResolver.Query(instancesUri,
			instancesColumns.ToArray(), selection, null, sortOrder)
			?? throw new CalendarStoreException("Error while querying events");

		// Confirm the calendar exists if no events were found
		if (cursor.Count == 0 && !string.IsNullOrEmpty(calendarId))
		{
			await GetCalendar(calendarId).ConfigureAwait(false);
		}

		return ToEventsFromInstances(cursor, instancesColumns).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !long.TryParse(eventId, out _))
		{
			throw InvalidEvent(eventId);
		}

		var calendarSpecificEvent =
			$"{CalendarContract.Events.InterfaceConsts.Id} = {eventId}";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			eventsColumns.ToArray(), calendarSpecificEvent, null, null)
			?? throw new CalendarStoreException("Error while querying events");

		if (cursor.Count <= 0)
		{
			throw InvalidEvent(eventId);
		}

		cursor.MoveToNext();
		var _event = ToEvent(cursor, eventsColumns);
		SafeCloseCursor(cursor);

		return _event;
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
		CreateEventCore(calendarEvent.CalendarId, calendarEvent.Title,
			calendarEvent.Description, calendarEvent.Location,
			calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.IsAllDay,
			calendarEvent.Reminders.ToArray(), calendarEvent.Recurrence, calendarEvent.TimeZoneId);

	async Task<string> CreateEventCore(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay, Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId)
	{
		if (string.IsNullOrEmpty(calendarId))
		{
			throw new CalendarStoreException("Calendar ID cannot be null or empty.");
		}

		if (string.IsNullOrEmpty(title))
		{
			throw new CalendarStoreException("Event title cannot be null or empty.");
		}

		if (recurrence is not null && !isAllDay && endDateTime <= startDateTime)
		{
			throw new CalendarStoreException(
				"The end date and time must be after the start date and time for a recurring event.");
		}

		await EnsureWriteCalendarPermission();

		// We just want to know a calendar with this ID exists,
		// but we also need to dispose the returned cursor.
		using var cursor = await GetPlatformCalendar(calendarId);

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		(startDateTime, endDateTime) = CalendarStore.ResolveStoredTimes(
			startDateTime, endDateTime, isAllDay, timeZone);

		ContentValues eventToInsert = new();

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
			startDateTime.ToUnixTimeMilliseconds());

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.EventTimezone,
			isAllDay ? "UTC" : CalendarStore.ResolveTimeZone(timeZoneId).Id);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.AllDay,
			isAllDay ? 1 : 0);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Title,
			title);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Description,
			description);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.EventLocation,
			location);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.CalendarId,
			calendarId);

		if (recurrence is not null)
		{
			// Recurring events use DURATION + RRULE instead of DTEND.
			var duration = CalendarStore.ComputeDuration(startDateTime, endDateTime, isAllDay);

			if (duration <= TimeSpan.Zero)
			{
				throw new CalendarStoreException(
					"The duration of a recurring event must be greater than zero.");
			}

			eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Duration,
				RecurrenceRuleParser.FormatDuration(duration));

			eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Rrule,
				RecurrenceRuleParser.ToRRule(recurrence));
		}
		else
		{
			eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Dtend,
				endDateTime.ToUnixTimeMilliseconds());
		}

		var idUrl = platformContentResolver?.Insert(eventsTableUri, eventToInsert);

		if (!long.TryParse(idUrl?.LastPathSegment, out var savedId))
		{
			throw new CalendarStoreException(
				"There was an error saving the event.");
		}

		// Add all reminders
		AddReminders(savedId, startDateTime, reminders);

		SafeCloseCursor(cursor);

		return savedId.ToString();
	}

	/// <inheritdoc/>
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location,
			startDate, endDate, true);
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
		await EnsureWriteCalendarPermission();

		if (!long.TryParse(eventId, out long platformEventId))
		{
			throw InvalidEvent(eventId);
		}

		var timeZone = isAllDay ? null : CalendarStore.ResolveEventTimeZone(timeZoneId);
		(startDateTime, endDateTime) = CalendarStore.ResolveStoredTimes(
			startDateTime, endDateTime, isAllDay, timeZone);

		switch (scope)
		{
			case RecurrenceScope.AllEvents:
				UpdateSeries(ResolveMasterId(platformEventId), title, description, location, startDateTime,
					endDateTime, isAllDay, reminders, timeZone);
				break;
			case RecurrenceScope.ThisEvent:
				UpdateOccurrence(platformEventId, title, description, location, startDateTime,
					endDateTime, isAllDay, reminders, RequireOccurrenceStart(originalOccurrenceStart));
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(scope), scope,
					"Unsupported recurrence scope.");
		}
	}

	void UpdateSeries(long eventId, string title, string description, string location,
		DateTimeOffset start, DateTimeOffset end, bool isAllDay, Reminder[]? reminders,
		TimeZoneInfo? timeZone)
	{
		var agenda = GetEventAgenda(eventId);

		var eventToUpdate = new ContentValues();
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtstart, start.ToUnixTimeMilliseconds());
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.AllDay, isAllDay ? 1 : 0);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Title, title);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Description, description);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.EventLocation, location);

		if (timeZone is not null)
		{
			eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.EventTimezone,
				isAllDay ? "UTC" : timeZone.Id);
		}

		if (string.IsNullOrWhiteSpace(agenda.Rrule))
		{
			eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtend, end.ToUnixTimeMilliseconds());
		}
		else
		{
			// Recurring series store a nominal duration instead of DTEND.
			eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Duration,
				RecurrenceRuleParser.FormatDuration(
					CalendarStore.ComputeDuration(start, end, isAllDay)));
		}

		var updateCount = platformContentResolver?.Update(
			ContentUris.WithAppendedId(eventsTableUri, eventId), eventToUpdate, null, null);

		if (updateCount != 1)
		{
			throw new CalendarStoreException("There was an error updating the event.");
		}

		RemoveAllReminders(eventId);

		if (reminders is not null)
		{
			AddReminders(eventId, start, reminders);
		}
	}

	void UpdateOccurrence(long eventId, string title, string description, string location,
		DateTimeOffset start, DateTimeOffset end, bool isAllDay, Reminder[]? reminders,
		DateTimeOffset originalOccurrenceStart)
	{
		var masterId = ResolveMasterId(eventId);
		var master = GetEventAgenda(masterId);

		// Android's provider only re-expands a series when applying an exception if the series
		// has a sync id (CalendarInstancesHelper#getRelevantRecurrenceEntries), which
		// locally-created calendars do not have. So exclude the original occurrence via EXDATE
		// and add the moved occurrence as a standalone event instead.
		ExcludeOccurrence(masterId, originalOccurrenceStart);

		var movedEventId = InsertStandaloneEvent(masterId, master.CalendarId, title, description, location,
			start, end, isAllDay, master.TimeZone, originalOccurrenceStart.ToUnixTimeMilliseconds());

		ReplaceReminders(movedEventId, start, reminders);
	}

	long ResolveMasterId(long eventId)
	{
		var agenda = GetEventAgenda(eventId);

		if (long.TryParse(agenda.OriginalId, out var masterId))
		{
			return masterId;
		}

		return TryParseDetachedMasterId(agenda.Uid2445, out var detachedMasterId)
			? detachedMasterId
			: eventId;
	}

	void ExcludeOccurrence(long masterId, DateTimeOffset occurrence)
	{
		var master = GetEventAgenda(masterId);

		// Drop any existing exception row for this occurrence so the master stays the source.
		var existingException = FindExceptionRowId(masterId, occurrence.ToUnixTimeMilliseconds());

		if (existingException is long exceptionId)
		{
			platformContentResolver?.Delete(
				ContentUris.WithAppendedId(eventsTableUri, exceptionId), null, null);
		}

		var detachedReplacement = FindDetachedReplacementId(
			masterId, occurrence.ToUnixTimeMilliseconds());

		if (detachedReplacement is long detachedId)
		{
			DeleteEventRow(detachedId, "There was an error replacing the occurrence.");
		}

		// The provider only re-expands the series when the update carries the recurrence
		// fields (it bails out early if DTSTART is missing), so include them alongside EXDATE.
		var values = new ContentValues();
		values.Put(CalendarContract.Events.InterfaceConsts.Dtstart, master.Dtstart);
		values.Put(CalendarContract.Events.InterfaceConsts.AllDay, master.AllDay ? 1 : 0);
		values.Put(CalendarContract.Events.InterfaceConsts.EventTimezone, GetTimezoneName(master.TimeZone));

		if (!string.IsNullOrEmpty(master.Rrule))
		{
			values.Put(CalendarContract.Events.InterfaceConsts.Rrule, master.Rrule);
		}

		if (!string.IsNullOrEmpty(master.Duration))
		{
			values.Put(CalendarContract.Events.InterfaceConsts.Duration, master.Duration);
		}

		values.Put(CalendarContract.Events.InterfaceConsts.Exdate,
			AppendExDate(master.Exdate, occurrence));

		var updateCount = platformContentResolver?.Update(
			ContentUris.WithAppendedId(eventsTableUri, masterId), values, null, null);

		if (updateCount != 1)
		{
			throw new CalendarStoreException("There was an error updating the occurrence.");
		}
	}

	long InsertStandaloneEvent(long masterId, string calendarId, string title, string description, string location,
		DateTimeOffset start, DateTimeOffset end, bool isAllDay, string? timeZone,
		long originalInstanceMillis)
	{
		var values = new ContentValues();
		values.Put(CalendarContract.Events.InterfaceConsts.CalendarId, calendarId);
		values.Put(CalendarContract.Events.InterfaceConsts.Dtstart, start.ToUnixTimeMilliseconds());
		values.Put(CalendarContract.Events.InterfaceConsts.Dtend, end.ToUnixTimeMilliseconds());
		// Remember which occurrence this standalone event replaces. It is not a provider
		// exception (we avoid those on local calendars), but it lets the read model report
		// OriginalOccurrenceStart/IsDetached just like the other platforms.
		values.Put(CalendarContract.Events.InterfaceConsts.OriginalInstanceTime, originalInstanceMillis);
		values.Put(CalendarContract.Events.InterfaceConsts.Uid2445,
			CreateDetachedUid(masterId, originalInstanceMillis));
		values.Put(CalendarContract.Events.InterfaceConsts.AllDay, isAllDay ? 1 : 0);
		values.Put(CalendarContract.Events.InterfaceConsts.EventTimezone,
			isAllDay ? "UTC" : GetTimezoneName(timeZone));
		values.Put(CalendarContract.Events.InterfaceConsts.Title, title);
		values.Put(CalendarContract.Events.InterfaceConsts.Description, description);
		values.Put(CalendarContract.Events.InterfaceConsts.EventLocation, location);

		var idUrl = platformContentResolver?.Insert(eventsTableUri, values)
			?? throw new CalendarStoreException("There was an error saving the occurrence.");

		if (!long.TryParse(idUrl.LastPathSegment, out var savedId))
		{
			throw new CalendarStoreException("There was an error saving the occurrence.");
		}

		return savedId;
	}

	static string AppendExDate(string? existing, DateTimeOffset occurrence)
	{
		var entry = occurrence.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

		return string.IsNullOrWhiteSpace(existing) ? entry : $"{existing},{entry}";
	}

	void ReplaceReminders(long eventId, DateTimeOffset start, Reminder[]? reminders)
	{
		RemoveAllReminders(eventId);

		if (reminders is not null)
		{
			AddReminders(eventId, start, reminders);
		}
	}

	static DateTimeOffset RequireOccurrenceStart(DateTimeOffset? originalOccurrenceStart) =>
		originalOccurrenceStart ?? throw new ArgumentException(
			"The original occurrence start is required to target a single occurrence.",
			nameof(originalOccurrenceStart));

	static string GetTimezoneName(string? timeZone) =>
		string.IsNullOrWhiteSpace(timeZone) ? TimeZoneInfo.Local.Id : timeZone;

	(string CalendarId, long Dtstart, string? TimeZone, bool AllDay, string? Rrule,
		string? OriginalId, string? Uid2445, string? Duration, string? Exdate)
		GetEventAgenda(long eventId)
	{
		var selection = $"{CalendarContract.Events.InterfaceConsts.Id} = ?";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			new[]
			{
				CalendarContract.Events.InterfaceConsts.CalendarId,
				CalendarContract.Events.InterfaceConsts.Dtstart,
				CalendarContract.Events.InterfaceConsts.EventTimezone,
				CalendarContract.Events.InterfaceConsts.AllDay,
				CalendarContract.Events.InterfaceConsts.Rrule,
				CalendarContract.Events.InterfaceConsts.OriginalId,
				CalendarContract.Events.InterfaceConsts.Uid2445,
				CalendarContract.Events.InterfaceConsts.Duration,
				CalendarContract.Events.InterfaceConsts.Exdate,
			},
			selection, new[] { eventId.ToString() }, null)
			?? throw new CalendarStoreException("Error while querying events");

		if (cursor.Count <= 0)
		{
			throw InvalidEvent(eventId.ToString());
		}

		cursor.MoveToNext();

		string? ReadString(string column)
		{
			var index = cursor.GetColumnIndexOrThrow(column);
			return cursor.IsNull(index) ? null : cursor.GetString(index);
		}

		var calendarId = ReadString(CalendarContract.Events.InterfaceConsts.CalendarId) ?? string.Empty;
		var dtstart = cursor.GetLong(cursor.GetColumnIndexOrThrow(
			CalendarContract.Events.InterfaceConsts.Dtstart));
		var timezone = ReadString(CalendarContract.Events.InterfaceConsts.EventTimezone);
		var allDay = cursor.GetInt(cursor.GetColumnIndexOrThrow(
			CalendarContract.Events.InterfaceConsts.AllDay)) != 0;
		var rrule = ReadString(CalendarContract.Events.InterfaceConsts.Rrule);
		var originalId = ReadString(CalendarContract.Events.InterfaceConsts.OriginalId);
		var uid2445 = ReadString(CalendarContract.Events.InterfaceConsts.Uid2445);
		var duration = ReadString(CalendarContract.Events.InterfaceConsts.Duration);
		var exdate = ReadString(CalendarContract.Events.InterfaceConsts.Exdate);

		return (calendarId, dtstart, timezone, allDay, rrule, originalId,
			uid2445, duration, exdate);
	}

	static string CreateDetachedUid(long masterId, long originalMillis) =>
		$"{detachedUidPrefix}{masterId.ToString(CultureInfo.InvariantCulture)}:" +
		$"{originalMillis.ToString(CultureInfo.InvariantCulture)}";

	static bool TryParseDetachedMasterId(string? uid, out long masterId)
	{
		masterId = default;

		if (string.IsNullOrEmpty(uid) || !uid.StartsWith(detachedUidPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		var separator = uid.IndexOf(':', detachedUidPrefix.Length);
		return separator > detachedUidPrefix.Length
			&& long.TryParse(uid.AsSpan(detachedUidPrefix.Length,
				separator - detachedUidPrefix.Length), NumberStyles.None,
				CultureInfo.InvariantCulture, out masterId);
	}

	List<long> GetDetachedEventIds(long masterId)
	{
		var selection = $"{CalendarContract.Events.InterfaceConsts.Uid2445} LIKE ?";
		var ids = new List<long>();

		using var cursor = platformContentResolver.Query(eventsTableUri,
			new[] { CalendarContract.Events.InterfaceConsts.Id }, selection,
			new[] { $"{detachedUidPrefix}{masterId.ToString(CultureInfo.InvariantCulture)}:%" }, null);

		if (cursor is null)
		{
			return ids;
		}

		while (cursor.MoveToNext())
		{
			ids.Add(cursor.GetLong(cursor.GetColumnIndexOrThrow(
				CalendarContract.Events.InterfaceConsts.Id)));
		}

		return ids;
	}

	long? FindDetachedReplacementId(long masterId, long originalMillis)
	{
		var selection = $"{CalendarContract.Events.InterfaceConsts.Uid2445} = ?";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			new[] { CalendarContract.Events.InterfaceConsts.Id }, selection,
			new[] { CreateDetachedUid(masterId, originalMillis) }, null);

		return cursor is not null && cursor.MoveToFirst()
			? cursor.GetLong(cursor.GetColumnIndexOrThrow(
				CalendarContract.Events.InterfaceConsts.Id))
			: null;
	}

	long? FindExceptionRowId(long masterId, long originalMillis)
	{
		var selection =
			$"{CalendarContract.Events.InterfaceConsts.OriginalId} = ? AND " +
			$"{CalendarContract.Events.InterfaceConsts.OriginalInstanceTime} = ?";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			new[] { CalendarContract.Events.InterfaceConsts.Id }, selection,
			new[] { masterId.ToString(), originalMillis.ToString() }, null)
			?? throw new CalendarStoreException("Error while querying events");

		if (cursor.Count <= 0)
		{
			return null;
		}

		cursor.MoveToNext();
		return cursor.GetLong(cursor.GetColumnIndexOrThrow(CalendarContract.Events.InterfaceConsts.Id));
	}

	void AddReminders(long eventId, DateTimeOffset eventStartDateTime, Reminder[]? reminders)
	{
		if (reminders is null || reminders.Length < 1)
		{
			return;
		}

		for (int i = 0; i < reminders.Length; i++)
		{
			var reminderValues = new ContentValues();
			reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.EventId, eventId);
			reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.Method, (int)RemindersMethod.Alert);
			reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.Minutes,
				(int)eventStartDateTime.Subtract(reminders[i].DateTime).TotalMinutes);

			_ = (platformContentResolver?.Insert(remindersTableUri, reminderValues))
				?? throw new CalendarStoreException("There was an error adding a reminder to the event.");
		}
	}

	void RemoveAllReminders(long eventId)
	{
		var selection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		var rowsDeleted = platformContentResolver?.Delete(
			remindersTableUri,
			selection,
			selectionArgs);

		if (rowsDeleted is null || rowsDeleted <= 0)
		{
			Console.WriteLine("No reminders were found to delete, or an error occurred.");
		}
		else
		{
			Console.WriteLine($"Deleted {rowsDeleted} reminder(s) for event ID {eventId}.");
		}
	}

	List<Reminder> GetAllEventReminders(long eventId)
	{
		return ToReminders(GetEventStartTime(eventId), GetEventReminderOffsets(eventId));
	}

	List<int> GetEventReminderOffsets(long eventId)
	{
		var selection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		var reminderOffsets = new List<int>();

		using var cursor = platformContentResolver?.Query(
			remindersTableUri,
			new[] { CalendarContract.Reminders.InterfaceConsts.Minutes },
			selection,
			selectionArgs,
			null);
		if (cursor is not null && cursor.MoveToFirst())
		{
			do
			{
				reminderOffsets.Add(cursor.GetInt(cursor.GetColumnIndexOrThrow(
					CalendarContract.Reminders.InterfaceConsts.Minutes)));
			}
			while (cursor.MoveToNext());

			SafeCloseCursor(cursor);
		}

		return reminderOffsets;
	}

	static List<Reminder> ToReminders(DateTimeOffset eventStart, IEnumerable<int> minuteOffsets) =>
		minuteOffsets.Select(minutes => new Reminder(eventStart.AddMinutes(-minutes))).ToList();

	DateTimeOffset GetEventStartTime(long eventId)
	{
		var selection = $"{CalendarContract.Events.InterfaceConsts.Id} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		using var cursor = platformContentResolver?.Query(
			eventsTableUri,
			new[] { CalendarContract.Events.InterfaceConsts.Dtstart },
			selection,
			selectionArgs,
			null);

		if (cursor is not null && cursor.MoveToFirst())
		{
			do
			{
				for (int i = 0; i < cursor.ColumnCount; i++)
				{
					Console.WriteLine($"{cursor.GetColumnName(i)}: {cursor.GetString(i)}");
				}
			} while (cursor.MoveToNext());
		}

		if (cursor is not null && cursor.MoveToFirst())
		{
			var startTimeMillis = cursor.GetLong(cursor.GetColumnIndexOrThrow(
				CalendarContract.Events.InterfaceConsts.Dtstart));

			SafeCloseCursor(cursor);

			return DateTimeOffset.FromUnixTimeMilliseconds(startTimeMillis)
				.ToLocalTime(); // Convert to local time

		}

		throw new CalendarStoreException($"Failed to retrieve start time for event with ID {eventId}.");
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
		// Android ids are always integers
		if (string.IsNullOrEmpty(eventId) ||
			!long.TryParse(eventId, out long platformEventId))
		{
			throw InvalidEvent(eventId);
		}

		await EnsureWriteCalendarPermission();

		if (scope == RecurrenceScope.AllEvents)
		{
			var seriesMasterId = ResolveMasterId(platformEventId);

			foreach (var detachedId in GetDetachedEventIds(seriesMasterId))
			{
				DeleteEventRow(detachedId, "There was an error deleting a detached occurrence.");
			}

			DeleteEventRow(seriesMasterId, "There was an error deleting the event.");
			return;
		}

		var occurrenceStart = RequireOccurrenceStart(originalOccurrenceStart);
		var masterId = ResolveMasterId(platformEventId);

		ExcludeOccurrence(masterId, occurrenceStart);
	}

	void DeleteEventRow(long eventId, string errorMessage)
	{
		var deleteCount = platformContentResolver?.Delete(
			ContentUris.WithAppendedId(eventsTableUri, eventId), null, null);

		if (deleteCount != 1)
		{
			throw new CalendarStoreException(errorMessage);
		}
	}

	static async Task EnsureWriteCalendarPermission()
	{
		var permissionResult = await Permissions.RequestAsync<Permissions.CalendarWrite>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException(
				"Permission for writing to calendar store is not granted.");
		}
	}

	List<CalendarEventAttendee> GetAttendees(string eventId)
	{
		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !long.TryParse(eventId, out _))
		{
			throw InvalidEvent(eventId);
		}

		var attendeeFilter =
			$"{CalendarContract.Attendees.InterfaceConsts.EventId}={eventId}";

		using var cursor = platformContentResolver.Query(attendeesTableUri,
			attendeesColumns.ToArray(), attendeeFilter, null, null)
			?? throw new CalendarStoreException("Error while querying attendees");

		return ToAttendees(cursor, attendeesColumns).ToList();
	}

	async Task<ICursor> GetPlatformCalendar(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!long.TryParse(calendarId, out _))
		{
			throw InvalidCalendar(calendarId);
		}

		var queryConditions =
			$"{CalendarContract.Calendars.InterfaceConsts.Deleted} != 1 AND " +
			$"{CalendarContract.Calendars.InterfaceConsts.Id} = {calendarId}";

		var cursor = platformContentResolver.Query(calendarsTableUri,
			calendarColumns.ToArray(), queryConditions, null, null)
			?? throw new CalendarStoreException("Error while querying calendars");

		try
		{
			if (cursor.Count <= 0)
			{
				throw InvalidCalendar(calendarId);
			}

			cursor.MoveToNext();

			return cursor;
		}
		catch
		{
			cursor.Dispose();
			throw;
		}
	}

	static IEnumerable<Calendar> ToCalendars(ICursor cursor, List<string> projection)
	{
		while (cursor.MoveToNext())
		{
			yield return ToCalendar(cursor, projection);
		}

		SafeCloseCursor(cursor);
	}

	static Calendar ToCalendar(ICursor cursor, List<string> projection)
	{
		var calendarColor = cursor.GetInt(projection.IndexOf(
			CalendarContract.Calendars.InterfaceConsts.CalendarColor));

		var virtualColor = GetDisplayColorFromColor(
			new Android.Graphics.Color(calendarColor)).AsColor();

		var platformCalendarReadOnly = cursor.GetInt(projection.IndexOf(
				CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel));

		var virtualCalendarReadOnly = platformCalendarReadOnly ==
			(int)CalendarAccess.AccessRead;

		return new(cursor.GetString(projection.IndexOf(
			CalendarContract.Calendars.InterfaceConsts.Id)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(
				CalendarContract.Calendars.InterfaceConsts.AccountName)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(
				CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName)) ?? string.Empty,
			virtualColor, virtualCalendarReadOnly);
	}

	// Android calendar does some magic on the actual calendar colors
	// See: https://github.com/aosp-mirror/platform_packages_apps_calendar/blob/66d2a697bb910421d4958073be16a0237faf3531/src/com/android/calendar/Utils.kt#L730
	static Android.Graphics.Color GetDisplayColorFromColor(Android.Graphics.Color color)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(4, 1))
		{
			return color;
		}

		var hsv = new float[3];

		Android.Graphics.Color.ColorToHSV(color, hsv);
		hsv[1] = Math.Min(hsv[1] * 1.3f, 1.0f);

		hsv[2] = hsv[2] * 0.8f;
		return Android.Graphics.Color.HSVToColor(hsv);
	}

	IEnumerable<CalendarEvent> ToEvents(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToEvent(cur, projection);
		}

		SafeCloseCursor(cur);
	}

	CalendarEvent ToEvent(ICursor cursor, List<string> projection)
	{
		var timezone = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.EventTimezone));
		var allDay = cursor.GetInt(projection.IndexOf(CalendarContract.Events.InterfaceConsts.AllDay)) != 0;
		var start = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtstart)));
		var end = GetEventEnd(cursor, projection, start);

		var rrule = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Rrule));
		var originalId = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.OriginalId));
		var originalInstanceTime = GetNullableLong(cursor, projection.IndexOf(
			CalendarContract.Events.InterfaceConsts.OriginalInstanceTime));

		var EventIDString = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty;
		if (!long.TryParse(EventIDString, out var eventId))
		{
			throw new CalendarStoreException($"Invalid Event ID: {EventIDString}");
		}

		var timeZone = CalendarStore.ResolveTimeZone(timezone);

		return new(EventIDString,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.CalendarId)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Title)) ?? string.Empty)
		{
			Description = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Description)) ?? string.Empty,
			Location = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.EventLocation)) ?? string.Empty,
			IsAllDay = allDay,
			StartDate = CalendarStore.FromUnixTimeMilliseconds(start.ToUnixTimeMilliseconds(), timeZone),
			EndDate = CalendarStore.FromUnixTimeMilliseconds(end.ToUnixTimeMilliseconds(), timeZone),
			TimeZoneId = allDay ? null : timezone,
			Recurrence = ParseRecurrence(rrule, allDay, timezone),
			IsDetached = !string.IsNullOrEmpty(originalId) || originalInstanceTime is not null,
			OriginalOccurrenceStart = originalInstanceTime is long original
				? CalendarStore.FromUnixTimeMilliseconds(original, timeZone)
				: null,
			EventColor = GetEventColor(cursor, projection),
			Attendees = GetAttendees(cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty).ToList(),
			Reminders = GetAllEventReminders(eventId),
		};
	}

	static DateTimeOffset GetEventEnd(ICursor cursor, List<string> projection, DateTimeOffset start)
	{
		var endIndex = projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtend);

		if (endIndex >= 0 && !cursor.IsNull(endIndex))
		{
			var end = cursor.GetLong(endIndex);
			if (end > 0)
			{
				return DateTimeOffset.FromUnixTimeMilliseconds(end);
			}
		}

		var durationIndex = projection.IndexOf(CalendarContract.Events.InterfaceConsts.Duration);
		if (durationIndex >= 0 && !cursor.IsNull(durationIndex)
			&& RecurrenceRuleParser.TryParseDuration(cursor.GetString(durationIndex), out var duration))
		{
			return start.Add(duration);
		}

		return start;
	}

	static long? GetNullableLong(ICursor cursor, int index)
	{
		if (index < 0 || cursor.IsNull(index))
		{
			return null;
		}

		return cursor.GetLong(index);
	}

	static CalendarRecurrence? ParseRecurrence(string? rrule, bool allDay, string? timezone)
	{
		if (string.IsNullOrWhiteSpace(rrule))
		{
			return null;
		}

		var anchor = allDay ? TimeZoneInfo.Utc : CalendarStore.ResolveTimeZone(timezone);
		return RecurrenceRuleParser.TryParse(rrule, anchor, out var recurrence) ? recurrence : null;
	}

	static Color? GetEventColor(ICursor cursor, List<string> projection)
	{
		var colorIndex = projection.IndexOf(CalendarContract.Events.InterfaceConsts.EventColor);
		if (colorIndex < 0)
		{
			return null;
		}

		var eventColorInt = cursor.GetInt(colorIndex);
		if (eventColorInt == 0)
		{
			return null;
		}

		return GetDisplayColorFromColor(new Android.Graphics.Color(eventColorInt)).AsColor();
	}

	IEnumerable<CalendarEvent> ToEventsFromInstances(ICursor cur, List<string> projection)
	{
		// Cache attendees and reminders by EventId to avoid redundant queries
		// for recurring events (which share the same EventId across occurrences).
		var attendeesCache = new Dictionary<string, List<CalendarEventAttendee>>();
		var reminderOffsetsCache = new Dictionary<long, List<int>>();

		while (cur.MoveToNext())
		{
			yield return ToEventFromInstance(cur, projection, attendeesCache, reminderOffsetsCache);
		}

		SafeCloseCursor(cur);
	}

	CalendarEvent ToEventFromInstance(ICursor cursor, List<string> projection,
		Dictionary<string, List<CalendarEventAttendee>> attendeesCache,
		Dictionary<long, List<int>> reminderOffsetsCache)
	{
		var timezone = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.EventTimezone));
		var allDay = cursor.GetInt(projection.IndexOf(CalendarContract.Events.InterfaceConsts.AllDay)) != 0;

		// Use Instances.Begin/End which reflect the actual occurrence times
		// (including recurring event expansions and externally synced changes)
		var startMillis = cursor.GetLong(projection.IndexOf(CalendarContract.Instances.Begin));
		var endMillis = cursor.GetLong(projection.IndexOf(CalendarContract.Instances.End));

		var eventIdString = cursor.GetString(projection.IndexOf(
			CalendarContract.Instances.EventId)) ?? string.Empty;

		if (!long.TryParse(eventIdString, out var eventId))
		{
			throw new CalendarStoreException($"Invalid Event ID: {eventIdString}");
		}

		var rrule = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Rrule));
		var originalId = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.OriginalId));
		var originalInstanceTime = GetNullableLong(cursor, projection.IndexOf(
			CalendarContract.Events.InterfaceConsts.OriginalInstanceTime));

		var timeZone = CalendarStore.ResolveTimeZone(timezone);
		var instanceStart = CalendarStore.FromUnixTimeMilliseconds(startMillis, timeZone);

		return new(eventIdString,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.CalendarId)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Title)) ?? string.Empty)
		{
			Description = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Description)) ?? string.Empty,
			Location = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.EventLocation)) ?? string.Empty,
			IsAllDay = allDay,
			StartDate = instanceStart,
			EndDate = CalendarStore.FromUnixTimeMilliseconds(endMillis, timeZone),
			TimeZoneId = allDay ? null : timezone,
			Recurrence = ParseRecurrence(rrule, allDay, timezone),
			IsDetached = !string.IsNullOrEmpty(originalId) || originalInstanceTime is not null,
			OriginalOccurrenceStart = CalendarStore.FromUnixTimeMilliseconds(
				originalInstanceTime ?? startMillis, timeZone),
			EventColor = GetEventColor(cursor, projection),
			Attendees = CalendarStore.GetOrAdd(attendeesCache, eventIdString,
				id => GetAttendees(id).ToList()),
			Reminders = ToReminders(instanceStart, CalendarStore.GetOrAdd(
				reminderOffsetsCache, eventId, id => GetEventReminderOffsets(id))),
		};
	}

	static IEnumerable<CalendarEventAttendee> ToAttendees(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToAttendee(cur, projection);
		}

		SafeCloseCursor(cur);
	}

	static CalendarEventAttendee ToAttendee(ICursor cur, List<string> attendeesProjection) =>
		new(cur.GetString(attendeesProjection.IndexOf(
			CalendarContract.Attendees.InterfaceConsts.AttendeeName)) ?? string.Empty,
			cur.GetString(attendeesProjection.IndexOf(
				CalendarContract.Attendees.InterfaceConsts.AttendeeEmail)) ?? string.Empty);

	static void SafeCloseCursor(ICursor? cursor)
	{
		if (cursor is not null && !cursor.IsClosed)
		{
			cursor.Close();
		}
	}
}
