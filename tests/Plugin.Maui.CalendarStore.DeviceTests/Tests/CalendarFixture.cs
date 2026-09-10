namespace Plugin.Maui.CalendarStore.DeviceTests;

/// <summary>
/// Creates a dedicated calendar for the device test run and removes it afterwards.
/// Permission is expected to be granted (CI pre-grants it; local runs prompt once).
/// </summary>
public sealed class CalendarFixture : IAsyncLifetime
{
	public ICalendarStore Store { get; } = CalendarStore.Default;

	public string CalendarId { get; private set; } = string.Empty;

	public async Task InitializeAsync()
	{
		var calendars = (await Store.GetCalendars()).ToList();

		// Reuse a leftover test calendar if a previous run crashed mid-way.
		var existing = calendars.FirstOrDefault(c =>
			c.Name.StartsWith("CalendarStore DeviceTests", StringComparison.Ordinal));

		CalendarId = existing?.Id ?? await Store.CreateCalendar(
			$"CalendarStore DeviceTests {Guid.NewGuid():N}");
	}

	public async Task DisposeAsync()
	{
		try
		{
			await Store.DeleteCalendar(CalendarId);
		}
		catch
		{
			// Best effort cleanup.
		}
	}
}
