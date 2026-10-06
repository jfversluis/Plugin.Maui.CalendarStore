![](nuget.png)

# Plugin.Maui.CalendarStore

`Plugin.Maui.CalendarStore` provides the ability to access the device calendar information in your .NET MAUI application.

## Install Plugin

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.CalendarStore.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.CalendarStore/)

Available on [NuGet](http://www.nuget.org/packages/Plugin.Maui.CalendarStore).

Install with the dotnet CLI: `dotnet add package Plugin.Maui.CalendarStore`, or through the NuGet Package Manager in Visual Studio.

## API Usage

`Plugin.Maui.CalendarStore` provides the `CalendarStore` class that has methods to retrieve calendars, events and attendee information from the device's calendar store.

You can either use it as a static class, e.g.: `CalendarStore.Default.GetCalendars()` or with dependency injection: `builder.Services.AddSingleton<ICalendarStore>(CalendarStore.Default);`

### Permissions

Before you can start using `CalendarStore`, you will need to request the proper permissions on each platform.

The runtime permission is automatically requested by the plugin when any of the methods is called.

#### iOS/macOS

For more information, please refer to the [official Apple documentation](https://developer.apple.com/documentation/eventkit/accessing_the_event_store#2975207).

##### iOS 17+

As of iOS 17, Apple has introduced a new layer of security for accessing calendar data. There is now a difference between only writing data to a calendar and obtain full access to calendar data. To use these, add the following keys to your `info.plist` file. When declared, the permission will be requested automatically at runtime.

```xml
<key>NSCalendarsWriteOnlyAccessUsageDescription</key>
<string>This app needs your permission to write events to your calendars. This includes creating events, but not reading, modifying or deleting events.</string>

<key>NSCalendarsFullAccessUsageDescription</key>
<string>This app needs your permission to fully manage events on your calendars. This includes reading, writing, modifying and deleting events.</string>
```

Remember, you always want to declare the least amount of permissions that are needed for your app. Else this might look suspicious to your users when you have something declared, but you're not using it. That means: leave out any key from above that is not applicable to your situation.

Additionally, make sure that the descriptions you provide are useful and describe why you want to access this data. Failing to do so might result in your app being rejected from the App Store.

If you also still want to support older iOS versions (prior to version 17), also follow the instructions below.

For a sandboxed macOS application, additionally you will have to add an entry to the `Entitlements.plist`, you will need to add the `com.apple.security.personal-information.calendar` key with a value of `true`.

See an example of a complete `Entitlements.plist` file below.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.app-sandbox</key>
    <true/>
    <key>com.apple.security.personal-information.calendars</key>
    <true/>
</dict>
</plist>
```

##### iOS < 17

On iOS and macOS prior to iOS 17, add the `NSCalendarsUsageDescription` key to your `info.plist` file. When declared, the permission will be requested automatically at runtime.

```xml
<key>NSCalendarsUsageDescription</key>
<string>This app wants to access your calendars</string>
```

If your app supports older iOS versions than iOS 17, include this key as well as the ones described above.

#### Android

On Android declare the `READ_CALENDAR` permission in your `AndroidManifest.xml` file for reading calendar information. If you also want to write information, also add the `WRITE_CALENDAR` permission.

This should be placed in the `manifest` node. You can also add this through the visual editor in Visual Studio.

The runtime permission is automatically requested by the plugin when any of the methods is called.

```xml
<uses-permission android:name="android.permission.READ_CALENDAR" />
<uses-permission android:name="android.permission.WRITE_CALENDAR" />
```

#### Windows

On Windows declare the `Appointments` permission in your `Package.appxmanifest` file.

This should be places in the `<Capabilities>` node, that is under the `<Package>` node.

The runtime permission is automatically requested by the plugin when any of the methods is called.

```xml
<uap:Capability Name="appointments"/>
`````

### Dependency Injection

You will first need to register the `Calendars` with the `MauiAppBuilder` following the same pattern that the .NET MAUI Essentials libraries follow.

```csharp
builder.Services.AddSingleton(CalendarStore.Default);
```

You can then enable your classes to depend on `ICalendarStore` as per the following example.

```csharp
public class CalendarsViewModel
{
    readonly ICalendarStore calendarStore;

    public CalendarsViewModel(ICalendarStore calendarStore)
    {
        this.calendarStore = calendarStore;
    }

    public async Task ReadCalendars()
    {
        var calendars = await calendarStore.GetCalendars();

        foreach (var c in calendars)
        {
            Console.WriteLine(c.Name);
        }
    }
}
```

### Straight usage

Alternatively if you want to skip using the dependency injection approach you can use the `Calendars.Default` property.

```csharp
public class CalendarsViewModel
{
    public async Task ReadCalendars()
    {
        var calendars = await CalendarStore.Default.GetCalendars();

        foreach (var c in calendars)
        {
            Console.WriteLine(c.Name);
        }
    }
}
```

### CalendarStore

Once you have created a `CalendarStore` instance you can interact with it in the following ways:

#### Methods

##### `IEnumerable<Calendars> GetCalendars()`

Retrieves all available calendars from the device.

##### `Calendar GetCalendar(string calendarId)`

Retrieves a specific calendar from the device.

##### `string CreateCalendar(string name, Color? color = null)`

Creates a new calendar on the device with the specified name and optionally color.
Returns the ID of the newly created calendar.

##### `UpdateCalendar(string calendarId, string newName, Color? newColor = null)`

Updates the calendar, specified by the unique identifier, with the given values.

<!--##### `DeleteCalendar(string calendarId)`

Removes a calendar, specified by the unique identifier, from the device.

##### `DeleteCalendar(Calendar calendarToDelete)`

Removes a calendar from the device.
This is basically just a convenience method that calls `DeleteCalendar` with `calendarToDelete.Id`.-->

##### `IEnumerable<CalendarEvent> GetEvents(string? calendarId = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)`

Retrieves events from a specific calendar or all calendars from the device.

##### `CalendarEvent GetEvent(string eventId)`

Retrieves a specific event from the calendar store on the device.

##### `string CreateEvent(string calendarId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false, Reminder[]? reminders = null)`

Creates an event in the specified calendar with the provided information. Optionally, one or more reminders can be attached to the event (see [Reminders](#reminders)). Returns the ID of the newly created event.

##### `string CreateEvent(string calendarId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, Reminder[]? reminders, CalendarRecurrence? recurrence, string? timeZoneId = null)`

Creates an event, optionally as a recurring series (see [Recurring events](#recurring-events)). When `recurrence` is provided the event repeats according to the rule. `timeZoneId` is the IANA time zone the series is anchored to and defaults to the device's local time zone. Returns the ID of the newly created event.

##### `string CreateEvent(CalendarEvent calendarEvent)`

Creates an event based on the information in the `CalendarEvent` object. This is basically just a convenience method that calls `CreateEvent` with all the unpacked information from `calendarEvent`.
Returns the ID of the newly created event.

##### `string CreateAllDayEvent(string calendarId, string title, string description, string location, DateTimeOffset startDate, DateTimeOffset endDate)`

Creates an all-day event in the specified calendar with the provided information. This is basically just a convenience method that calls `CreateEvent` with `isAllDay` set to true.
Returns the ID of the newly created event.

##### `UpdateEvent(string eventId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay, Reminder[]? reminders = null)`

Updates an event, specified by the unique identifier, on the device calendar. If `reminders` is provided, all existing reminders on the event will be replaced with the ones in the provided collection. If `reminders` is `null`, all existing reminders will be cleared.

##### `UpdateEvent(CalendarEvent eventToUpdate)`

Updates an event on the device calendar.
This is basically just a convenience method that calls `UpdateEvent` with all the details provided from `eventToUpdate`.

#### Reminders

Events can have reminders attached to them. A `Reminder` represents a notification that alerts the user before an event starts. Each reminder has a `DateTime` property that specifies when the reminder should fire.

```csharp
// Create an event with two reminders: 1 hour and 15 minutes before
var eventStart = new DateTimeOffset(2025, 6, 15, 14, 0, 0, TimeSpan.Zero);
var eventEnd = eventStart.AddHours(1);

var reminders = new[]
{
    new Reminder(eventStart.AddHours(-1)),   // 1 hour before
    new Reminder(eventStart.AddMinutes(-15)) // 15 minutes before
};

var eventId = await calendarStore.CreateEvent(
    calendarId, "Team Meeting", "Weekly sync", "Conference Room",
    eventStart, eventEnd, reminders: reminders);
```

Reminders attached to an event can be read back through the `CalendarEvent.Reminders` property:

```csharp
var calendarEvent = await calendarStore.GetEvent(eventId);

foreach (var reminder in calendarEvent.Reminders)
{
    Console.WriteLine($"Reminder at: {reminder.DateTime}");
}
```

> **Note:** On Windows, only a single reminder per event is supported. If multiple reminders are provided, only the first one will be used.

#### Recurring events

An event can repeat by supplying a `CalendarRecurrence`. It maps to `EKRecurrenceRule` on iOS/macOS, the `RRULE`/`DURATION` columns on Android, and `AppointmentRecurrence` on Windows.

```csharp
var start = new DateTimeOffset(2025, 6, 2, 9, 0, 0, TimeSpan.Zero); // Monday
var end = start.AddHours(1);

var recurrence = new CalendarRecurrence
{
    Frequency = RecurrenceFrequency.Weekly,
    Interval = 1,
    DaysOfWeek = { new(DayOfWeek.Monday), new(DayOfWeek.Wednesday) },
    Count = 10, // or set Until instead
};

var eventId = await calendarStore.CreateEvent(
    calendarId, "Standup", "Twice-weekly sync", "Meeting room",
    start, end, recurrence: recurrence, timeZoneId: "America/New_York");
```

The rule is anchored to the **wall-clock** time of `start` in `timeZoneId`, so a 09:00 series stays at 09:00 local time across daylight saving changes. When `timeZoneId` is `null` the device's local time zone is used.

Supported `CalendarRecurrence` members:

| Member | Description |
| ------ | ----------- |
| `Frequency` | `Daily`, `Weekly`, `Monthly` or `Yearly`. |
| `Interval` | How many frequency units between occurrences (default 1). |
| `Count` / `Until` | When the series ends. These are mutually exclusive. |
| `FirstDayOfWeek` | Day treated as the start of the week (iOS/macOS cannot set this). |
| `DaysOfWeek` | Days of the week, optionally with an ordinal (e.g. `3` = third, `-1` = last). |
| `DaysOfMonth` | Days of the month (1–31, negative counts from the end). |
| `MonthsOfYear` | Months of the year (1–12). |
| `WeeksOfYear` / `DaysOfYear` | Weeks (1–53) / days (1–366) of the year. |
| `SetPositions` | Ordinal filter within the period (e.g. `-1` = last). |

A rule can also be round-tripped to and from its iCalendar `RRULE` representation, which is useful when persisting or exchanging recurrence data:

```csharp
var rrule = recurrence.ToRRule(); // "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE"

if (CalendarRecurrence.TryParse(rrule, out var parsed))
{
    // parsed holds the same rule
}

// Or use Parse, which throws FormatException for invalid input:
var strictlyParsed = CalendarRecurrence.Parse("FREQ=DAILY;COUNT=5");
```

`TryParse`/`Parse` accept an optional `TimeZoneInfo` used to interpret non-UTC `UNTIL` values; when omitted the device's local time zone is used.

When events are retrieved with `GetEvents`, recurring events are expanded into individual occurrences within the requested range. Each returned `CalendarEvent` exposes `IsRecurring`, `Recurrence`, `TimeZoneId`, `IsDetached`, and `OriginalOccurrenceStart` (the original slot of the occurrence).

#### Exceptions (single occurrences)

Use the scope-aware overloads to change or remove a single occurrence or the whole series:

```csharp
// Remove one occurrence identified by OriginalOccurrenceStart
await calendarStore.DeleteEvent(occurrenceId, RecurrenceScope.ThisEvent,
    occurrence.OriginalOccurrenceStart);

// Move one occurrence of a series
await calendarStore.UpdateEvent(occurrenceId, "Standup (moved)", "", "Meeting room",
    newStart, newEnd, false, reminders: null,
    scope: RecurrenceScope.ThisEvent,
    originalOccurrenceStart: occurrence.OriginalOccurrenceStart);
```

`RecurrenceScope` values are `ThisEvent` and `AllEvents`. The existing parameter overloads of `UpdateEvent`/`DeleteEvent` apply to the **whole series**.

> **Note:** The original occurrence start is required when targeting a single occurrence.
>
> **Android:** for calendars/events without a sync id, native `CalendarProvider` recurrence exceptions are not reliably expanded — its exception matching is built around `ORIGINAL_SYNC_ID`, and the local `ORIGINAL_ID` path is incomplete (`getRelevantRecurrenceEntries` still selects the master by `_id`). Single-occurrence edits therefore use `EXDATE` on the recurring master and, for modified occurrences, a standalone replacement event. The replacement stores the original occurrence time in `ORIGINAL_INSTANCE_TIME` (with neither `ORIGINAL_ID` nor `ORIGINAL_SYNC_ID`), so the provider treats it as an ordinary event while the library still exposes `OriginalOccurrenceStart`/`IsDetached`.

#### Time zones

Events expose a `TimeZoneId` (IANA identifier, or `null` for a floating/local event). For recurring events this is the anchor used to repeat the wall-clock time. On iOS/macOS the platform value is an `EKEvent.TimeZone`; on Windows the time zone is stored on the recurrence rule. Windows does not store a time zone for non-recurring appointments.

##### `DeleteEvent(string eventId)`

Removes an event, specified by the unique identifier, from the device calendar.

##### `DeleteEvent(CalendarEvent eventToDelete)`

Removes an event from the device calendar.
This is basically just a convenience method that calls `DeleteEvent` with `eventToDelete.Id`.

## Testing

### Unit tests

Shared, platform-independent logic (recurrence parsing, time-zone helpers and the static facade) is covered by host tests:

```bash
dotnet test tests/Plugin.Maui.CalendarStore.Tests/Plugin.Maui.CalendarStore.Tests.csproj
```

### Device tests

The platform implementations are validated on a real device or simulator with [DeviceRunners](https://mattleibow.github.io/DeviceRunners/). The test app in `tests/Plugin.Maui.CalendarStore.DeviceTests` exercises calendars and recurring-event CRUD, including single-occurrence exceptions.

Calendar access is required, so grant the permission before running headlessly.

iOS simulator:

```bash
xcrun simctl boot "iPhone 16"
xcrun simctl privacy booted grant calendar com.jfversluis.pluginmauicalendarstore.devicetests
dotnet test tests/Plugin.Maui.CalendarStore.DeviceTests/Plugin.Maui.CalendarStore.DeviceTests.csproj -f net10.0-ios
```

Android emulator:

```bash
adb install -r -g <path-to-signed.apk>
dotnet test tests/Plugin.Maui.CalendarStore.DeviceTests/Plugin.Maui.CalendarStore.DeviceTests.csproj -f net10.0-android
```

The same runs are executed in CI by `.github/workflows/ci-device-tests.yml`.

# Acknowledgements

This project could not have came to be without these projects and people, thank you! <3

## Xamarin.Essentials PR

There are a couple of people involved in bringing this functionality to Xamarin.Essentials. Unfortunately the [PR](https://github.com/xamarin/Essentials/pull/1384) was never merged. In an attempt not to let this code go to waste, I transformed it into this library. Thank you [@mattleibow](https://github.com/mattleibow), [@nickrandolph](https://github.com/nickrandolph), [@ScottBTR](https://github.com/ScottBTR) and [@mkieres](https://github.com/mkieres) for the initial work here!
