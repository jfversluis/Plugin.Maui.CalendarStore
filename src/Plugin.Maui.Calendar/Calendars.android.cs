using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.Calendar;

partial class FeatureImplementation : ICalendars
{
	public async Task<IEnumerable<Calendar>> GetCalendarsAsync()
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendarsUri = CalendarContract.Calendars.ContentUri;
		var calendarsProjection = new List<string>
		{
			CalendarContract.Calendars.InterfaceConsts.Id,
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName
		};

		var queryConditions =
			$"{CalendarContract.Calendars.InterfaceConsts.Deleted} != 1";

		using var cur = Platform.AppContext.ApplicationContext.ContentResolver.Query(calendarsUri,
			calendarsProjection.ToArray(), queryConditions, null, null);

		return ToCalendars(cur, calendarsProjection).ToList();
	}

	public async Task<Calendar> GetCalendarAsync(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!int.TryParse(calendarId, out _))
		{
			throw Calendars.InvalidCalendar(calendarId);
		}

		var calendarsUri = CalendarContract.Calendars.ContentUri;
		var calendarsProjection = new List<string>
		{
			CalendarContract.Calendars.InterfaceConsts.Id,
			CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName
		};
		var queryConditions =
			$"{CalendarContract.Calendars.InterfaceConsts.Deleted} != 1 AND " +
			$"{CalendarContract.Calendars.InterfaceConsts.Id} = {calendarId}";

		using var cur = Platform.AppContext.ApplicationContext.ContentResolver.Query(calendarsUri,
			calendarsProjection.ToArray(), queryConditions, null, null);

		if (cur.Count <= 0)
		{
			throw Calendars.InvalidCalendar(calendarId);
		}

		cur.MoveToNext();

		return ToCalendar(cur, calendarsProjection);
	}

	public async Task<IEnumerable<CalendarEvent>> GetEventsAsync(string calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(calendarId) && !int.TryParse(calendarId, out _))
		{
			throw Calendars.InvalidCalendar(calendarId);
		}

		var sDate = startDate ?? DateTimeOffset.Now.Add(Calendars.defaultStartTimeFromNow);
		var eDate = endDate ?? sDate.Add(Calendars.defaultEndTimeFromStartTime);

		var eventsUri = CalendarContract.Events.ContentUri;
		var eventsProjection = new List<string>
		{
			CalendarContract.Events.InterfaceConsts.Id,
			CalendarContract.Events.InterfaceConsts.CalendarId,
			CalendarContract.Events.InterfaceConsts.Title,
			CalendarContract.Events.InterfaceConsts.Description,
			CalendarContract.Events.InterfaceConsts.EventLocation,
			CalendarContract.Events.InterfaceConsts.AllDay,
			CalendarContract.Events.InterfaceConsts.Dtstart,
			CalendarContract.Events.InterfaceConsts.Dtend,
			CalendarContract.Events.InterfaceConsts.Deleted
		};

		var calendarSpecificEvent =
			$"{CalendarContract.Events.InterfaceConsts.Dtend} >= {sDate.AddMilliseconds(sDate.Offset.TotalMilliseconds).ToUnixTimeMilliseconds()} AND " +
			$"{CalendarContract.Events.InterfaceConsts.Dtstart} <= {eDate.AddMilliseconds(sDate.Offset.TotalMilliseconds).ToUnixTimeMilliseconds()} AND " +
			$"{CalendarContract.Events.InterfaceConsts.Deleted} != 1 ";

		if (!string.IsNullOrEmpty(calendarId))
		{
			calendarSpecificEvent += $" AND {CalendarContract.Events.InterfaceConsts.CalendarId} = {calendarId}";
		}

		var sortOrder = $"{CalendarContract.Events.InterfaceConsts.Dtstart} ASC";

		using var cur = Platform.AppContext.ApplicationContext.ContentResolver.Query(eventsUri, eventsProjection.ToArray(), calendarSpecificEvent, null, sortOrder);

		// confirm the calendar exists if no events were found
		// the PlatformGetCalendarAsync wll throw if not
		if (cur.Count == 0 && !string.IsNullOrEmpty(calendarId))
		{
			await GetCalendarAsync(calendarId).ConfigureAwait(false);
		}

		return ToEvents(cur, eventsProjection).ToList();
	}

	public async Task<CalendarEvent> GetEventAsync(string eventId)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !int.TryParse(eventId, out _))
		{
			throw Calendars.InvalidCalendar(eventId);
		}

		var eventsUri = CalendarContract.Events.ContentUri;
		var eventsProjection = new List<string>
		{
			CalendarContract.Events.InterfaceConsts.Id,
			CalendarContract.Events.InterfaceConsts.CalendarId,
			CalendarContract.Events.InterfaceConsts.Title,
			CalendarContract.Events.InterfaceConsts.Description,
			CalendarContract.Events.InterfaceConsts.EventLocation,
			CalendarContract.Events.InterfaceConsts.AllDay,
			CalendarContract.Events.InterfaceConsts.Dtstart,
			CalendarContract.Events.InterfaceConsts.Dtend
		};

		var calendarSpecificEvent = $"{CalendarContract.Events.InterfaceConsts.Id} = {eventId}";

		using var cur = Platform.AppContext.ApplicationContext.ContentResolver.Query(eventsUri, eventsProjection.ToArray(), calendarSpecificEvent, null, null);

		if (cur.Count <= 0)
		{
			throw Calendars.InvalidEvent(eventId);
		}

		cur.MoveToNext();

		return ToEvent(cur, eventsProjection);
	}

	IEnumerable<CalendarEventAttendee> GetAttendees(string eventId)
	{
		// Android ids are always integers
		if (!string.IsNullOrEmpty(eventId) && !int.TryParse(eventId, out _))
		{
			throw Calendars.InvalidCalendar(eventId);
		}

		var attendeesUri = CalendarContract.Attendees.ContentUri;
		var attendeesProjection = new List<string>
		{
			CalendarContract.Attendees.InterfaceConsts.EventId,
			CalendarContract.Attendees.InterfaceConsts.AttendeeEmail,
			CalendarContract.Attendees.InterfaceConsts.AttendeeName
		};
		var attendeeSpecificAttendees =
			$"{CalendarContract.Attendees.InterfaceConsts.EventId}={eventId}";

		using var cur = Platform.AppContext.ApplicationContext.ContentResolver.Query(attendeesUri, attendeesProjection.ToArray(), attendeeSpecificAttendees, null, null);

		return ToAttendees(cur, attendeesProjection).ToList();
	}

	IEnumerable<Calendar> ToCalendars(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToCalendar(cur, projection);
		}
	}

	static Calendar ToCalendar(ICursor cur, List<string> projection) =>
		new(cur.GetString(projection.IndexOf(CalendarContract.Calendars.InterfaceConsts.Id)),
			cur.GetString(projection.IndexOf(CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName)));

	IEnumerable<CalendarEvent> ToEvents(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToEvent(cur, projection);
		}
	}

	CalendarEvent ToEvent(ICursor cur, List<string> projection)
	{
		var allDay = cur.GetInt(projection.IndexOf(CalendarContract.Events.InterfaceConsts.AllDay)) != 0;
		var start = DateTimeOffset.FromUnixTimeMilliseconds(cur.GetLong(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtstart)));
		var end = DateTimeOffset.FromUnixTimeMilliseconds(cur.GetLong(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Dtend)));

		return new(cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id)),
			cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.CalendarId)),
			cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Title)))
		{
			Description = cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Description)),
			Location = cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.EventLocation)),
			AllDay = allDay,
			StartDate = start,
			EndDate = end,
			Attendees = GetAttendees(cur.GetString(projection.IndexOf(CalendarContract.Events.InterfaceConsts.Id))).ToList()
		};
	}

	IEnumerable<CalendarEventAttendee> ToAttendees(ICursor cur, List<string> projection)
	{
		while (cur.MoveToNext())
		{
			yield return ToAttendee(cur, projection);
		}
	}

	static CalendarEventAttendee ToAttendee(ICursor cur, List<string> attendeesProjection) =>
		new(cur.GetString(attendeesProjection.IndexOf(CalendarContract.Attendees.InterfaceConsts.AttendeeName)),
			cur.GetString(attendeesProjection.IndexOf(CalendarContract.Attendees.InterfaceConsts.AttendeeEmail)));
}