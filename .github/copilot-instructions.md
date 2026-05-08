# Plugin.Maui.CalendarStore — Copilot Instructions

## Project Overview

This is a .NET MAUI plugin that provides cross-platform access to device calendars (read/write calendars, events, attendees, reminders). It targets Android, iOS, macOS (Catalyst), and Windows.

## Architecture

### Partial Class Pattern

The core implementation uses **partial classes** with platform-specific files:

- `*.shared.cs` — Shared code compiled on all platforms (models, interfaces, static facade)
- `*.android.cs` — Android implementation using `CalendarContract` content provider
- `*.macios.cs` — iOS and macOS implementation using `EventKit` framework
- `*.windows.cs` — Windows implementation using `AppointmentStore` API
- `*.ios.cs` — iOS-only code (e.g., permissions for iOS 17+)
- `*.net.cs` — Generic .NET stub (throws `NotImplementedException`)

The main implementation class is `CalendarStoreImplementation : ICalendarStore`, defined as a partial class across these files.

### File Naming Convention

Always follow this pattern when creating new files:
- `FeatureName.shared.cs` for cross-platform code
- `FeatureName.android.cs` for Android-specific code
- `FeatureName.macios.cs` for combined iOS/macOS code
- `FeatureName.windows.cs` for Windows-specific code
- `FeatureName.ios.cs` for iOS-only code
- `FeatureName.net.cs` for generic .NET fallback

### Adapter Pattern

Platform-specific types are adapted to shared models:
- Android: `ICursor` → `Calendar`, `CalendarEvent`
- iOS/macOS: `EKCalendar`, `EKEvent` → `Calendar`, `CalendarEvent`
- Windows: `AppointmentCalendar`, `Appointment` → `Calendar`, `CalendarEvent`

Each platform has `ToCalendar()` and `ToEvent()` static helper methods for this conversion.

### Static Facade + DI

The plugin supports two usage patterns:
```csharp
// Static access
CalendarStore.Default.GetCalendars();

// Dependency injection
builder.Services.AddSingleton(CalendarStore.Default);
```

## Code Conventions

### Namespace
All code uses a single namespace: `Plugin.Maui.CalendarStore`

### Naming
- `camelCase` for private/internal fields (prefixed with nothing, no `_`)
- `PascalCase` for public members
- File-scoped namespaces (enforced as error)
- Accessibility modifiers omitted when default

### XML Documentation
- **Required** on all public APIs (classes, methods, properties, parameters)
- Use `<inheritdoc/>` on platform implementations and static facade methods
- Include `<exception>` tags documenting thrown exceptions
- Include `<remarks>` for platform-specific limitations (e.g., Windows only supports 1 reminder)

### Error Handling
1. **Always check permissions first** using `Permissions.RequestAsync<T>()`
2. Throw `PermissionException` for denied permissions
3. Throw `CalendarStoreException` for calendar-specific errors
4. Throw `ArgumentException` for invalid IDs (use `ArgumentException.ThrowIfNullOrEmpty()`)
5. Use null-coalescing with throw: `?? throw new CalendarStoreException("message")`

### Null Safety
- Nullable is enabled project-wide
- Use `?? string.Empty` when reading platform values that may be null
- Use null-conditional operators (`?.`) when accessing platform objects that may be null at runtime even if typed as non-nullable (common in ObjC/Swift interop)

## Platform-Specific Notes

### Android
- Uses `ContentResolver` to query `CalendarContract` tables
- Event IDs are `long` integers (converted to/from string)
- Color values use HSV conversion
- Timezone handling wraps exceptions with fallback

### iOS/macOS
- Uses `EKEventStore` singleton
- Calendar source selection priority: CalDav > Local > Default
- iOS 17+ has separate write-only and full-access permissions
- `EKCalendar.Source` can be null at runtime despite non-nullable typing — always use `?.`

### Windows
- Uses `AppointmentStore` API
- **Only 1 reminder supported per event** (platform limitation)
- `SourceDisplayName` may be empty for local calendars

## Testing

When making changes:
1. Ensure the plugin builds: `dotnet build src/Plugin.Maui.CalendarStore/Plugin.Maui.CalendarStore.csproj -c Release`
2. Ensure the sample builds: `dotnet build samples/Plugin.Maui.CalendarStore.Sample/Plugin.Maui.CalendarStore.Sample.csproj -c Release`
3. If adding a new public API, add it to `ICalendarStore.shared.cs` with full XML docs
4. Implement on all platforms (android, macios, windows) and add a `NotImplementedException` stub in `.net.cs`
5. Update the sample app to demonstrate the new feature
6. Update `README.md` if the change affects the public API

## Project Structure

```
src/Plugin.Maui.CalendarStore/          # Plugin library
  Plugin.Maui.CalendarStore.csproj      # Multi-targeting .NET 9 project
  ICalendarStore.shared.cs              # Public interface (source of truth for API)
  CalendarStore.shared.cs               # Static facade
  CalendarStore.android.cs              # Android implementation (~780 lines)
  CalendarStore.macios.cs               # iOS/macOS implementation (~460 lines)
  CalendarStore.windows.cs              # Windows implementation (~380 lines)
  CalendarStore.net.cs                  # Generic .NET stubs
  Calendar.shared.cs                    # Calendar model
  CalendarEvent.shared.cs               # Event model
  CalendarEventAttendee.shared.cs       # Attendee model
  Reminder.shared.cs                    # Reminder model

samples/Plugin.Maui.CalendarStore.Sample/  # Demo MAUI app
  CalendarsPage.xaml[.cs]               # Calendar listing
  EventsPage.xaml[.cs]                  # Event listing
  AddEventsPage.xaml[.cs]               # Event creation
```
