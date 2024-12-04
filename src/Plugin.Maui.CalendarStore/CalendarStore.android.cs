using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using static Plugin.Maui.CalendarStore.CalendarStore;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	readonly Android.Net.Uri calendarsTableUri;
	readonly Android.Net.Uri eventsTableUri;
	readonly Android.Net.Uri attendeesTableUri;

	readonly ContentResolver platformContentResolver;

	readonly List<string> calendarColumns =
		[
			CalendarContract.Calendars.InterfaceConsts.Id,
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
			CalendarContract.Calendars.InterfaceConsts.CalendarColor,
			CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel,
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
			CalendarContract.Events.InterfaceConsts.Deleted,
			CalendarContract.Events.InterfaceConsts.EventTimezone,
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
		var cursor = await GetPlatformCalendar(calendarId);

		return ToCalendar(cursor, calendarColumns);
	}

	/// <inheritdoc/>
	public async Task<string> CreateCalendar(string name, Color? color = null)
	{
		await EnsureWriteCalendarPermission();

		ContentValues calendarToCreate = new();

		calendarToCreate.Put(
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
			name);

		if (color is not null)
		{
			calendarToCreate.Put(CalendarContract.Calendars.InterfaceConsts.CalendarColor,
				color.AsColor());
		}

		var idUrl = platformContentResolver?.Insert(calendarsTableUri,
			calendarToCreate);

		if (!long.TryParse(idUrl?.LastPathSegment, out var savedId))
		{
			throw new CalendarStoreException(
				"There was an error saving the calendar.");
		}

		return savedId.ToString();
	}

	/// <inheritdoc/>
	public async Task UpdateCalendar(string calendarId, string newName, Color? newColor = null)
	{
		await EnsureWriteCalendarPermission();

		ContentValues calendarToUpdate = new();

		// We just want to know a calendar with this ID exists
		_ = await GetPlatformCalendar(calendarId);

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

		if (updateCount != 1)
		{
			throw new CalendarStoreException(
				"There was an error updating the calendar.");
		}
	}

	///// <inheritdoc/>
	//public async Task DeleteCalendar(string calendarId)
	//{
	//	await EnsureWriteCalendarPermission();

	//	// Android ids are always integers
	//	if (string.IsNullOrEmpty(calendarId) ||
	//		!long.TryParse(calendarId, out long platformCalendarId))
	//	{
	//		throw InvalidCalendar(calendarId);
	//	}

	//	// We just want to know a calendar with this ID exists
	//	_ = await GetPlatformCalendar(calendarId);

	//	var deleteEventUri = ContentUris.WithAppendedId(calendarsTableUri, platformCalendarId);
	//	var deleteCount = platformContentResolver?.Delete(deleteEventUri, null, null);

	//	if (deleteCount != 1)
	//	{
	//		throw new CalendarStoreException(
	//			"There was an error deleting the calendar.");
	//	}
	//}

	///// <inheritdoc/>
	//public Task DeleteCalendar(Calendar calendarToDelete) =>
	//	DeleteCalendar(calendarToDelete.Id);

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

		var calendarSpecificEvent =
			$"{CalendarContract.Events.InterfaceConsts.Dtend} >= " +
			$"{sDate.AddMilliseconds(sDate.Offset.TotalMilliseconds).ToUnixTimeMilliseconds()} AND " +
			$"{CalendarContract.Events.InterfaceConsts.Dtstart} <= " +
			$"{eDate.AddMilliseconds(sDate.Offset.TotalMilliseconds).ToUnixTimeMilliseconds()} AND " +
			$"{CalendarContract.Events.InterfaceConsts.Deleted} != 1 ";

		if (!string.IsNullOrEmpty(calendarId))
		{
			calendarSpecificEvent += $" AND {CalendarContract.Events.InterfaceConsts.CalendarId}" +
				$" = {calendarId}";
		}

		var sortOrder = $"{CalendarContract.Events.InterfaceConsts.Dtstart} ASC";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			eventsColumns.ToArray(), calendarSpecificEvent, null, sortOrder)
			?? throw new CalendarStoreException("Error while querying events");

		// Confirm the calendar exists if no events were found
		if (cursor.Count == 0 && !string.IsNullOrEmpty(calendarId))
		{
			await GetCalendar(calendarId).ConfigureAwait(false);
		}

		return ToEvents(cursor, eventsColumns).ToList();
	}

	/// <inheritdoc/>
	public async Task<CalendarEvent> GetEvent(string eventId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !long.TryParse(eventId, out _))
		{
			throw InvalidCalendar(eventId);
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

		return ToEvent(cursor, eventsColumns);
	}

	/// <inheritdoc/>
	public async Task<string> CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay = false, Reminder[]? reminders = null)
	{
		if (string.IsNullOrEmpty(calendarId))
		{
			throw new CalendarStoreException("Calendar ID cannot be null or empty.");
		}
		if (string.IsNullOrEmpty(title))
		{
			throw new CalendarStoreException("Event title cannot be null or empty.");
		}
		await EnsureWriteCalendarPermission();

		// We just want to know a calendar with this ID exists
		_ = await GetPlatformCalendar(calendarId);

		ContentValues eventToInsert = new();
		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
			startDateTime.ToUnixTimeMilliseconds());

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Dtend,
			endDateTime.ToUnixTimeMilliseconds());

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.EventTimezone,
			TimeZoneInfo.Local.StandardName);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.AllDay,
			isAllDay);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Title,
			title);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.Description,
			description);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.EventLocation,
			location);

		eventToInsert.Put(CalendarContract.Events.InterfaceConsts.CalendarId,
			calendarId);

		var idUrl = platformContentResolver?.Insert(eventsTableUri, eventToInsert);

		if (!long.TryParse(idUrl?.LastPathSegment, out var savedId))
		{
			throw new CalendarStoreException(
				"There was an error saving the event.");
		}
		// Add all reminders
		AddMultipleReminders(savedId, reminders);

		return savedId.ToString();


	}

	/// <inheritdoc/>
	public Task<string> CreateEvent(CalendarEvent calendarEvent)
	{
		return CreateEvent(calendarEvent.CalendarId, calendarEvent.Title,
			calendarEvent.Description, calendarEvent.Location,
			calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.IsAllDay, calendarEvent.Reminders.ToArray());
	}

	/// <inheritdoc/>
	public Task<string> CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location,
			startDate, endDate, true);
	}

	/// <inheritdoc/>
	public async Task UpdateEvent(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay,
		Reminder[]? reminders = null)
	{
		await EnsureWriteCalendarPermission();

		using var cursor = platformContentResolver.Query(
			eventsTableUri, calendarColumns.ToArray(), null, null, null)
			?? throw new CalendarStoreException("Error while querying events");

		long platformEventId = 0;

		if (!long.TryParse(eventId, out long virtualEventId))
		{
			throw InvalidEvent(eventId);
		}

		while (cursor.MoveToNext())
		{
			long id = cursor.GetLong(cursor.GetColumnIndex(calendarColumns[0]));

			if (id == virtualEventId)
			{
				platformEventId = cursor.GetLong(
					cursor.GetColumnIndex(calendarColumns[0]));

				break;
			}
		}

		if (platformEventId <= 0)
		{
			throw new CalendarStoreException("Could not determine platform event ID.");
		}

		ContentValues eventToUpdate = new();
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
			startDateTime.ToUnixTimeMilliseconds());

		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtend,
			endDateTime.ToUnixTimeMilliseconds());

		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.AllDay,
			isAllDay);

		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Title,
			title);

		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Description,
			description);

		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.EventLocation,
			location);

		var updateCount = platformContentResolver?.Update(
			ContentUris.WithAppendedId(eventsTableUri, platformEventId), eventToUpdate, null, null);

		if (updateCount != 1)
		{
			throw new CalendarStoreException(
				"There was an error updating the event.");
		}

		RemoveAllReminders(platformEventId);

		if (reminders is not null)
		{
			AddMultipleReminders(platformEventId, reminders);
		}
	}
	//void RemoveAllReminders(long eventId)
	public async Task UpdateEventWithReminder(string eventId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, int reminderMinutes)
	{
		await EnsureWriteCalendarPermission();

		var selection = $"{CalendarContract.Events.InterfaceConsts.Id} = ?";
		var selectionArgs = new[] { eventId };

		using var cursor = platformContentResolver.Query(
			eventsTableUri, calendarColumns.ToArray(), selection, selectionArgs, null)
			?? throw new CalendarStoreException("Error while querying events");

		if (!cursor.MoveToFirst())
		{
			throw new CalendarStoreException($"Event with ID {eventId} was not found.");
		}

		long platformEventId = cursor.GetLong(cursor.GetColumnIndex(calendarColumns[0]));

		ContentValues eventToUpdate = new();
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
			startDateTime.ToUnixTimeMilliseconds());
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Dtend,
			endDateTime.ToUnixTimeMilliseconds());
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.AllDay, isAllDay);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Title, title);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.Description, description);
		eventToUpdate.Put(CalendarContract.Events.InterfaceConsts.EventLocation, location);

		var updateCount = platformContentResolver?.Update(
			ContentUris.WithAppendedId(eventsTableUri, platformEventId), eventToUpdate, null, null);

		if (updateCount != 1)
		{
			throw new CalendarStoreException($"Failed to update event with ID {eventId}.");
		}

		UpdateOrAddReminder(platformEventId, reminderMinutes);
	}

	void UpdateOrAddReminder(long eventId, int reminderMinutes)
	{
		var reminderSelection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var reminderSelectionArgs = new[] { eventId.ToString() };

		platformContentResolver?.Delete(CalendarContract.Reminders.ContentUri, reminderSelection, reminderSelectionArgs);
	}

	/// <inheritdoc/>
	public Task UpdateEvent(CalendarEvent eventToUpdate) =>
		UpdateEvent(eventToUpdate.Id, eventToUpdate.Title, eventToUpdate.Description,
			eventToUpdate.Location, eventToUpdate.StartDate, eventToUpdate.EndDate, eventToUpdate.IsAllDay, eventToUpdate.Reminders.ToArray());

	/// <inheritdoc/>
	void AddMultipleReminders(long eventId, Reminder[]? reminders)
	{
		if (reminders is null && reminders?.Length < 1)
		{
			return; //nothing to be done.
		}

		var existingReminders = GetExistingReminders(eventId);

		int index = 0;

		foreach (var reminder in reminders)
		{
			// Add a new reminder
			AddSingleReminder(eventId, reminder.ReminderInMinutes);
		}

		// Remove any extra existing reminders not present in the new list
		for (int i = index; i < existingReminders.Count; i++)
		{
			RemoveSingleReminder(existingReminders[i]);
		}
	}

	public void RemoveSingleReminder(long eventId)
	{
		var reminderSelection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var reminderSelectionArgs = new[] { eventId.ToString() };

		var rowsDeleted = platformContentResolver?.Delete(
			CalendarContract.Reminders.ContentUri, reminderSelection, reminderSelectionArgs);

		if (rowsDeleted == null || rowsDeleted < 1)
		{
			// No reminder found, no need to throw error.
			// Simply nothing was removed.
			return;
		}

		//if it returns here, then reminder was removed.
		//if you need a confirmation of sorts, make it return int for count or so
	}
	void RemoveAllReminders(long eventId)
	{
		var selection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		var rowsDeleted = platformContentResolver?.Delete(
			CalendarContract.Reminders.ContentUri,
			selection,
			selectionArgs);

		if (rowsDeleted == null || rowsDeleted <= 0)
		{
			Console.WriteLine("No reminders were found to delete, or an error occurred.");
		}
		else
		{
			Console.WriteLine($"Deleted {rowsDeleted} reminder(s) for event ID {eventId}.");
		}
	}

	void AddSingleReminder(long eventId, int reminderMinutes)
	{
		var reminderValues = new ContentValues();
		reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.EventId, eventId);
		reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.Method, (int)RemindersMethod.Alert);
		reminderValues.Put(CalendarContract.Reminders.InterfaceConsts.Minutes, reminderMinutes);

		var reminderUri = platformContentResolver?.Insert(CalendarContract.Reminders.ContentUri, reminderValues);
		if (reminderUri == null)
		{
			throw new CalendarStoreException("There was an error adding a reminder to the event.");
		}
	}

	List<Reminder> GetAllEventReminders(long eventId)
	{
		// Query to fetch all reminders for the event
		var selection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		var reminderTimes = new List<Reminder>();

		using var cursor = platformContentResolver?.Query(
			CalendarContract.Reminders.ContentUri,
			new[] { CalendarContract.Reminders.InterfaceConsts.Minutes },
			selection,
			selectionArgs,
			null);
		int reminderMinutes = 0;
		if (cursor != null && cursor.MoveToFirst())
		{
			// Retrieve the event start time
			var eventStartTime = GetEventStartTime(eventId);

			do
			{
				reminderMinutes = cursor.GetInt(cursor.GetColumnIndexOrThrow(CalendarContract.Reminders.InterfaceConsts.Minutes));
				Reminder remindr = new(eventStartTime.AddMinutes(-reminderMinutes))
				{
					ReminderInMinutes = reminderMinutes,
				};
				reminderTimes.Add(remindr);
			}
			while (cursor.MoveToNext());
		}

		return reminderTimes;
	}
	DateTimeOffset GetEventStartTime(long eventId)
	{
		var selection = $"{CalendarContract.Events.InterfaceConsts.Id} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		using var cursor = platformContentResolver?.Query(
			CalendarContract.Events.ContentUri,
			new[] { CalendarContract.Events.InterfaceConsts.Dtstart },
			selection,
			selectionArgs,
			null);
		if (cursor != null && cursor.MoveToFirst())
		{
			do
			{
				for (int i = 0; i < cursor.ColumnCount; i++)
				{
					Console.WriteLine($"{cursor.GetColumnName(i)}: {cursor.GetString(i)}");
				}
			} while (cursor.MoveToNext());
		}
		if (cursor != null && cursor.MoveToFirst())
		{
			//var startTimeMillis = cursor.GetLong(cursor.GetColumnIndexOrThrow(CalendarContract.Events.InterfaceConsts.Dtstart));

			var startTimeMillis = cursor.GetLong(cursor.GetColumnIndexOrThrow(CalendarContract.Events.InterfaceConsts.Dtstart));
			return DateTimeOffset.FromUnixTimeMilliseconds(startTimeMillis)
										   .ToLocalTime(); // Convert to local time

		}

		throw new CalendarStoreException($"Failed to retrieve start time for event with ID {eventId}.");
	}

	List<long> GetExistingReminders(long eventId)
	{
		if (platformContentResolver == null)
		{
			throw new InvalidOperationException("PlatformContentResolver is not initialized.");
		}
		var reminderIds = new List<long>();
		var selection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var selectionArgs = new[] { eventId.ToString() };

		using var cursor = platformContentResolver.Query(
			CalendarContract.Reminders.ContentUri,
			new[] { CalendarContract.Reminders.InterfaceConsts.Id },
			selection,
			selectionArgs,
			null);

		if (cursor != null && cursor.MoveToFirst())
		{
			do
			{
				try
				{
					var reminderId = cursor.GetLong(cursor.GetColumnIndexOrThrow(CalendarContract.Reminders.InterfaceConsts.Id));
					reminderIds.Add(reminderId);
				}
				catch (Exception ex)
				{
					// Log or handle the exception if needed
					Console.WriteLine($"Error retrieving reminder ID: {ex.Message}");
				}
			}
			while (cursor.MoveToNext());
		}

		return reminderIds;
	}



	/// <inheritdoc/>
	public async Task DeleteEvent(string eventId)
	{
		await EnsureWriteCalendarPermission();

		// Android ids are always integers
		if (string.IsNullOrEmpty(eventId) ||
			!long.TryParse(eventId, out long platformEventId))
		{
			throw InvalidEvent(eventId);
		}

		ContentValues eventToRemove = new();
		eventToRemove.Put(
			CalendarContract.Events.InterfaceConsts.Id, platformEventId);

		var deleteCount = platformContentResolver?.Delete(
			ContentUris.WithAppendedId(eventsTableUri, platformEventId), null, null);

		if (deleteCount != 1)
		{
			throw new CalendarStoreException(
				"There was an error deleting the event.");
		}
	}


	/// <inheritdoc/>
	public Task DeleteEvent(CalendarEvent eventToDelete) =>
		DeleteEvent(eventToDelete.Id);

	static async Task EnsureWriteCalendarPermission()
	{
		var permissionResult = await Permissions.RequestAsync<Permissions.CalendarWrite>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException(
				"Permission for writing to calendar store is not granted.");
		}
	}

	IEnumerable<CalendarEventAttendee> GetAttendees(string eventId)
	{
		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !long.TryParse(eventId, out _))
		{
			throw InvalidCalendar(eventId);
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

		using var cursor = platformContentResolver.Query(calendarsTableUri,
			calendarColumns.ToArray(), queryConditions, null, null)
			?? throw new CalendarStoreException("Error while querying calendars");

		if (cursor.Count <= 0)
		{
			throw InvalidCalendar(calendarId);
		}

		cursor.MoveToNext();

		return cursor;
	}

	static IEnumerable<Calendar> ToCalendars(ICursor cursor, List<string> projection)
	{
		while (cursor.MoveToNext())
		{
			yield return ToCalendar(cursor, projection);
		}
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
	}
	CalendarEvent ToEvent(ICursor cursor, List<string> projection)
	{
		var timezone = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.EventTimezone));
		var allDay = cursor.GetInt(projection.IndexOf(CalendarContract.Events.InterfaceConsts.AllDay)) != 0;
		var start = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtstart)));
		var end = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtend)));

		DateTimeOffset SafeConvertTime(DateTimeOffset time, string tz)
		{
			try
			{
				return tz is null ? time : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, tz);
			}
			catch (TimeZoneNotFoundException)
			{
				return time; // Fallback if timezone is invalid
			}
		}
		var EventIDString = cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty;
		if (!long.TryParse(EventIDString, out var eventId))
		{
			throw new CalendarStoreException($"Invalid Event ID: {EventIDString}");
		}
		return new(EventIDString,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.CalendarId)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Title)) ?? string.Empty)
		{
			Description = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Description)) ?? string.Empty,
			Location = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.EventLocation)) ?? string.Empty,
			IsAllDay = allDay,
			StartDate = timezone is null ? start : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(start, timezone),
			EndDate = timezone is null ? end : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(end, timezone),
			Attendees = GetAttendees(cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty).ToList(),
			// Retrieve reminder minutes for the event
			MinutesBeforeReminder = GetEventReminderInMinutes(cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty),
			Reminders = GetAllEventReminders(eventId),
		};
	}
	int GetEventReminderInMinutes(string eventId)
	{
		var reminderProjection = new[] { CalendarContract.Reminders.InterfaceConsts.Minutes };
		var reminderSelection = $"{CalendarContract.Reminders.InterfaceConsts.EventId} = ?";
		var reminderSelectionArgs = new[] { eventId };
		var reminderCursor = platformContentResolver?.Query(
			CalendarContract.Reminders.ContentUri,
			reminderProjection,
			reminderSelection,
			reminderSelectionArgs,
			null);

		int minutes = 0; // Default to 0 if no reminder is found

		if (reminderCursor != null && reminderCursor.MoveToFirst())
		{
			minutes = reminderCursor.GetInt(reminderCursor.GetColumnIndex(reminderProjection[0]));
			reminderCursor.Close();
		}

		return minutes;
	}
	static IEnumerable<CalendarEventAttendee> ToAttendees(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToAttendee(cur, projection);
		}
	}

	static CalendarEventAttendee ToAttendee(ICursor cur, List<string> attendeesProjection) =>
		new(cur.GetString(attendeesProjection.IndexOf(
			CalendarContract.Attendees.InterfaceConsts.AttendeeName)) ?? string.Empty,
			cur.GetString(attendeesProjection.IndexOf(
				CalendarContract.Attendees.InterfaceConsts.AttendeeEmail)) ?? string.Empty);
}