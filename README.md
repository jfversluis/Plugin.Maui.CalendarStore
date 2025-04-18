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

##### `string CreateEvent(string calendarId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay = false)`

Creates an event in the specified calendar with the provided information. Returns the ID of the newly created event.

##### `string CreateEvent(CalendarEvent calendarEvent)`

Creates an event based on the information in the `CalendarEvent` object. This is basically just a convenience method that calls `CreateEvent` with all the unpacked information from `calendarEvent`.
Returns the ID of the newly created event.

##### `string CreateAllDayEvent(string calendarId, string title, string description, string location, DateTimeOffset startDate, DateTimeOffset endDate)`

Creates an all-day event in the specified calendar with the provided information. This is basically just a convenience method that calls `CreateEvent` with `isAllDay` set to true.
Returns the ID of the newly created event.

##### `UpdateEvent(string eventId, string title, string description, string location, DateTimeOffset startDateTime, DateTimeOffset endDateTime, bool isAllDay)`

Updates an event, specified by the unique identifier, on the device calendar.

##### `UpdateEvent(CalendarEvent eventToUpdate)`

Updates an event on the device calendar.
This is basically just a convenience method that calls `UpdateEvent` with all the details provided from `eventToUpdate`.

##### `DeleteEvent(string eventId)`

Removes an event, specified by the unique identifier, from the device calendar.

##### `DeleteEvent(CalendarEvent eventToDelete)`

Removes an event from the device calendar.
This is basically just a convenience method that calls `DeleteEvent` with `eventToDelete.Id`.

# Acknowledgements

This project could not have came to be without these projects and people, thank you! <3

## Xamarin.Essentials PR

There are a couple of people involved in bringing this functionality to Xamarin.Essentials. Unfortunately the [PR](https://github.com/xamarin/Essentials/pull/1384) was never merged. In an attempt not to let this code go to waste, I transformed it into this library. Thank you [@mattleibow](https://github.com/mattleibow), [@nickrandolph](https://github.com/nickrandolph), [@ScottBTR](https://github.com/ScottBTR) and [@mkieres](https://github.com/mkieres) for the initial work here!
