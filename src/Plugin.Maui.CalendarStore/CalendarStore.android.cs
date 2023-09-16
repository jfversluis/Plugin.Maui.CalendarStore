using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.CalendarStore;

partial class CalendarStoreImplementation : ICalendarStore
{
	readonly Android.Net.Uri calendarsTableUri;
	readonly Android.Net.Uri eventsTableUri;
	readonly Android.Net.Uri attendeesTableUri;

	readonly ContentResolver platformContentResolver;

	readonly List<string> calendarColumns = new()
		{
			CalendarContract.Calendars.InterfaceConsts.Id,
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
		};

	readonly List<string> eventsColumns = new()
		{
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
		};

	readonly List<string> attendeesColumns = new()
		{
			CalendarContract.Attendees.InterfaceConsts.EventId,
			CalendarContract.Attendees.InterfaceConsts.AttendeeEmail,
			CalendarContract.Attendees.InterfaceConsts.AttendeeName,
		};

	public CalendarStoreImplementation()
	{
		calendarsTableUri = CalendarContract.Calendars.ContentUri
			?? throw new CalendarStore.CalendarStoreException(
				"Could not determine Android calendars table URI.");

		eventsTableUri = CalendarContract.Events.ContentUri
			?? throw new CalendarStore.CalendarStoreException(
				"Could not determine Android events table URI.");

		attendeesTableUri = CalendarContract.Attendees.ContentUri
			?? throw new CalendarStore.CalendarStoreException(
				"Could not determine Android attendees table URI.");

		platformContentResolver = Platform.AppContext.ApplicationContext?.ContentResolver
			?? throw new CalendarStore.CalendarStoreException(
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
			?? throw new CalendarStore.CalendarStoreException("Error while querying calendars");

		return ToCalendars(cursor, calendarColumns).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!long.TryParse(calendarId, out _))
		{
			throw CalendarStore.InvalidCalendar(calendarId);
		}

		var queryConditions =
			$"{CalendarContract.Calendars.InterfaceConsts.Deleted} != 1 AND " +
			$"{CalendarContract.Calendars.InterfaceConsts.Id} = {calendarId}";

		using var cursor = platformContentResolver.Query(calendarsTableUri,
			calendarColumns.ToArray(), queryConditions, null, null)
			?? throw new CalendarStore.CalendarStoreException("Error while querying calendars");

		if (cursor.Count <= 0)
		{
			throw CalendarStore.InvalidCalendar(calendarId);
		}

		cursor.MoveToNext();

		return ToCalendar(cursor, calendarColumns);
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(
		string? calendarId = null, DateTimeOffset? startDate = null,
		DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(calendarId) && !int.TryParse(calendarId, out _))
		{
			throw CalendarStore.InvalidCalendar(calendarId);
		}

		var sDate = startDate ?? DateTimeOffset.Now.Add(CalendarStore.defaultStartTimeFromNow);
		var eDate = endDate ?? sDate.Add(CalendarStore.defaultEndTimeFromStartTime);		

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
			?? throw new CalendarStore.CalendarStoreException("Error while querying events");

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
			throw CalendarStore.InvalidCalendar(eventId);
		}

		var calendarSpecificEvent =
			$"{CalendarContract.Events.InterfaceConsts.Id} = {eventId}";

		using var cursor = platformContentResolver.Query(eventsTableUri,
			eventsColumns.ToArray(), calendarSpecificEvent, null, null)
			?? throw new CalendarStore.CalendarStoreException("Error while querying events");

		if (cursor.Count <= 0)
		{
			throw CalendarStore.InvalidEvent(eventId);
		}

		cursor.MoveToNext();

		return ToEvent(cursor, eventsColumns);
	}

	/// <inheritdoc/>
	public async Task CreateEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime,
		bool isAllDay = false)
	{
		var permissionResult = await Permissions.RequestAsync<Permissions.CalendarWrite>();

		if (permissionResult != PermissionStatus.Granted)
		{
			throw new PermissionException("Permission for writing to calendar store is not granted.");
		}

		using var cursor = platformContentResolver.Query(
			calendarsTableUri, calendarColumns.ToArray(), null, null, null);

		long platformCalendarId = 0;

		if (cursor != null)
		{
			if (!int.TryParse(calendarId, out int virtualCalendarId))
			{
				throw CalendarStore.InvalidCalendar(calendarId);
			}

			while (cursor.MoveToNext())
			{
				long id = cursor.GetLong(cursor.GetColumnIndex(calendarColumns[0]));

				if (id == virtualCalendarId)
				{
					platformCalendarId = cursor.GetLong(
						cursor.GetColumnIndex(calendarColumns[0]));

					break;
				}
			}

			if (platformCalendarId != 0)
			{
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
					platformCalendarId);

				var idUrl = platformContentResolver?.Insert(eventsTableUri, eventToInsert);

				if (!long.TryParse(idUrl?.LastPathSegment, out _))
				{
					throw new CalendarStore.CalendarStoreException("There was an error saving the event.");
				}
			}
		}
	}

	/// <inheritdoc/>
	public Task CreateEvent(CalendarEvent calendarEvent)
	{
		return CreateEvent(calendarEvent.CalendarId, calendarEvent.Title,
			calendarEvent.Description, calendarEvent.Location,
			calendarEvent.StartDate, calendarEvent.EndDate, calendarEvent.AllDay);
	}

	/// <inheritdoc/>
	public Task CreateAllDayEvent(string calendarId, string title, string description,
		string location, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		return CreateEvent(calendarId, title, description, location,
			startDate, endDate, true);
	}

	IEnumerable<CalendarEventAttendee> GetAttendees(string eventId)
	{
		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !int.TryParse(eventId, out _))
		{
			throw CalendarStore.InvalidCalendar(eventId);
		}

		var attendeeFilter =
			$"{CalendarContract.Attendees.InterfaceConsts.EventId}={eventId}";

		using var cursor = platformContentResolver.Query(attendeesTableUri,
			attendeesColumns.ToArray(), attendeeFilter, null, null)
			?? throw new CalendarStore.CalendarStoreException("Error while querying attendees");

		return ToAttendees(cursor, attendeesColumns).ToList();
	}

	static IEnumerable<Calendar> ToCalendars(ICursor cursor, List<string> projection)
	{
		while (cursor.MoveToNext())
		{
			yield return ToCalendar(cursor, projection);
		}
	}

	static Calendar ToCalendar(ICursor cur, List<string> projection) =>
		new(cur.GetString(projection.IndexOf(
			CalendarContract.Calendars.InterfaceConsts.Id)) ?? string.Empty,
			cur.GetString(projection.IndexOf(
				CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName)) ?? string.Empty);

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

		return new(cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.CalendarId)) ?? string.Empty,
			cursor.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Title)) ?? string.Empty)
		{
			Description = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Description)) ?? string.Empty,
			Location = cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.EventLocation)) ?? string.Empty,
			AllDay = allDay,
			StartDate = timezone is null ? start : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(start, timezone),
			EndDate = timezone is null ? end : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(end, timezone),
			Attendees = GetAttendees(cursor.GetString(projection.IndexOf(
				CalendarContract.Events.InterfaceConsts.Id)) ?? string.Empty).ToList()
		};
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