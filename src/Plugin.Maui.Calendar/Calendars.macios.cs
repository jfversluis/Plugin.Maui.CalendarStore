using EventKit;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.Calendar;

partial class FeatureImplementation : ICalendars
{
	static EKEventStore? eventStore;

	static EKEventStore EventStore =>
		eventStore ??= new EKEventStore();

	public async Task<IEnumerable<Calendar>> GetCalendarsAsync()
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		return ToCalendars(calendars).ToList();
	}

	public async Task<Calendar> GetCalendarAsync(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		var calendar = calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);

		if (calendar is null)
		{
			throw Calendars.InvalidCalendar(calendarId);
		}

		return ToCalendar(calendar);
	}

	public async Task<IEnumerable<CalendarEvent>> GetEventsAsync(string calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var startDateToConvert = startDate ?? DateTimeOffset.Now.Add(Calendars.defaultStartTimeFromNow);
		var endDateToConvert = endDate ?? startDateToConvert.Add(Calendars.defaultEndTimeFromStartTime); // NOTE: 4 years is the maximum period that a iOS calendar events can search

		var sDate = NSDate.FromTimeIntervalSince1970(TimeSpan.FromMilliseconds(startDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);
		var eDate = NSDate.FromTimeIntervalSince1970(TimeSpan.FromMilliseconds(endDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		if (!string.IsNullOrEmpty(calendarId))
		{
			var calendar = calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);

			if (calendar is null)
			{
				throw Calendars.InvalidCalendar(calendarId);
			}

			calendars = new[] { calendar };
		}

		var query = EventStore.PredicateForEvents(sDate, eDate, calendars);
		var events = EventStore.EventsMatching(query);

		return ToEvents(events).OrderBy(e => e.StartDate).ToList();
	}

	public Task<CalendarEvent> GetEventAsync(string eventId)
	{
		if (!(EventStore.GetCalendarItem(eventId) is EKEvent calendarEvent))
		{
			throw Calendars.InvalidEvent(eventId);
		}

		return Task.FromResult(ToEvent(calendarEvent));
	}

	public IEnumerable<Calendar> ToCalendars(IEnumerable<EKCalendar> native)
	{
		foreach (var calendar in native)
		{
			yield return ToCalendar(calendar);
		}
	}

	Calendar ToCalendar(EKCalendar calendar) =>
    	new()
		{
			Id = calendar.CalendarIdentifier,
			Name = calendar.Title
		};

	IEnumerable<CalendarEvent> ToEvents(IEnumerable<EKEvent> native)
	{
		foreach (var e in native)
		{
			yield return ToEvent(e);
		}
	}

	CalendarEvent ToEvent(EKEvent native) =>
		new()
		{
			Id = native.CalendarItemIdentifier,
			CalendarId = native.Calendar.CalendarIdentifier,
			Title = native.Title,
			Description = native.Notes,
			Location = native.Location,
			AllDay = native.AllDay,
			StartDate = DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(native.StartDate.SecondsSince1970),
			EndDate = DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(native.EndDate.SecondsSince1970),
			Attendees = native.Attendees != null
				? ToAttendees(native.Attendees).ToList()
				: new List<CalendarEventAttendee>()
		};

	IEnumerable<CalendarEventAttendee> ToAttendees(IEnumerable<EKParticipant> inviteList)
	{
		foreach (var attendee in inviteList)
		{
			yield return new CalendarEventAttendee
			{
				Name = attendee.Name,
				Email = attendee.Name
			};
		}
	}
}