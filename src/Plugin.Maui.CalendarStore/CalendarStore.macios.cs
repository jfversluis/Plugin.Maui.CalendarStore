using EventKit;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.CalendarStore;

partial class FeatureImplementation : ICalendarStore
{
	static EKEventStore? eventStore;

	static EKEventStore EventStore =>
		eventStore ??= new EKEventStore();

	/// <inheritdoc/>
	public async Task<IEnumerable<Calendar>> GetCalendars()
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		return ToCalendars(calendars).ToList();
	}

	/// <inheritdoc/>
	public async Task<Calendar> GetCalendar(string calendarId)
	{
		ArgumentException.ThrowIfNullOrEmpty(calendarId);

		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		var calendar = calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);

		return calendar is null ? throw CalendarStore.InvalidCalendar(calendarId) : ToCalendar(calendar);
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<CalendarEvent>> GetEvents(string? calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
	{
		await Permissions.RequestAsync<Permissions.CalendarRead>();

		var startDateToConvert = startDate ?? DateTimeOffset.Now.Add(CalendarStore.defaultStartTimeFromNow);
		var endDateToConvert = endDate ?? startDateToConvert.Add(CalendarStore.defaultEndTimeFromStartTime); // NOTE: 4 years is the maximum period that a iOS calendar events can search

		var sDate = NSDate.FromTimeIntervalSince1970(TimeSpan.FromMilliseconds(startDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);
		var eDate = NSDate.FromTimeIntervalSince1970(TimeSpan.FromMilliseconds(endDateToConvert.ToUnixTimeMilliseconds()).TotalSeconds);

		var calendars = EventStore.GetCalendars(EKEntityType.Event);

		if (!string.IsNullOrEmpty(calendarId))
		{
			var calendar = calendars.FirstOrDefault(c => c.CalendarIdentifier == calendarId);

			if (calendar is null)
			{
				throw CalendarStore.InvalidCalendar(calendarId);
			}

			calendars = new[] { calendar };
		}

		var query = EventStore.PredicateForEvents(sDate, eDate, calendars);
		var events = EventStore.EventsMatching(query);

		return ToEvents(events).OrderBy(e => e.StartDate).ToList();
	}

	/// <inheritdoc/>
	public Task<CalendarEvent> GetEvent(string eventId)
	{
		if (!(EventStore.GetCalendarItem(eventId) is EKEvent calendarEvent))
		{
			throw CalendarStore.InvalidEvent(eventId);
		}

		return Task.FromResult(ToEvent(calendarEvent));
	}

	static IEnumerable<Calendar> ToCalendars(IEnumerable<EKCalendar> native)
	{
		foreach (var calendar in native)
		{
			yield return ToCalendar(calendar);
		}
	}

	static Calendar ToCalendar(EKCalendar calendar) =>
    	new(calendar.CalendarIdentifier, calendar.Title);

	static IEnumerable<CalendarEvent> ToEvents(IEnumerable<EKEvent> native)
	{
		foreach (var e in native)
		{
			yield return ToEvent(e);
		}
	}

	static CalendarEvent ToEvent(EKEvent platform) =>
		new(platform.CalendarItemIdentifier,
			platform.Calendar.CalendarIdentifier,
			platform.Title)
		{
			Description = platform.Notes,
			Location = platform.Location,
			AllDay = platform.AllDay,
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
			yield return new(attendee.Name, attendee.Name);
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
}