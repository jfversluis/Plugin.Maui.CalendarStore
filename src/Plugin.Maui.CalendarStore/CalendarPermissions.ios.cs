using EventKit;
using Foundation;

namespace Plugin.Maui.CalendarStore;

public class WriteOnlyCalendar : Permissions.BasePlatformPermission
{
	protected override Func<IEnumerable<string>> RequiredInfoPlistKeys =>
		() => new string[] { "NSCalendarsWriteOnlyAccessUsageDescription" };

	/// <inheritdoc/>
	public override Task<PermissionStatus> CheckStatusAsync()
	{
		EnsureDeclared();

		return Task.FromResult(EventPermission.CheckPermissionStatus(EKEntityType.Event));
	}

	/// <inheritdoc/>

	public override async Task<PermissionStatus> RequestAsync()
	{
		EnsureDeclared();

		var status = EventPermission.CheckPermissionStatus(EKEntityType.Event);
		if (status == PermissionStatus.Granted)
		{
			return status;
		}

		var eventStore = new EKEventStore();

		bool granted;

		if (OperatingSystem.IsIOSVersionAtLeast(17, 0))
		{
			(granted, _) = await eventStore.RequestWriteOnlyAccessToEventsAsync();
		}
		else
		{
			(granted, _) = await eventStore.RequestAccessAsync(EKEntityType.Event);
		}

		return granted ? PermissionStatus.Granted : PermissionStatus.Denied;
	}
}

public class FullAccessCalendar : Permissions.BasePlatformPermission
{
	protected override Func<IEnumerable<string>> RequiredInfoPlistKeys =>
		() => new string[] { "NSCalendarsFullAccessUsageDescription" };

	/// <inheritdoc/>
	public override Task<PermissionStatus> CheckStatusAsync()
	{
		EnsureDeclared();

		return Task.FromResult(EventPermission.CheckPermissionStatus(EKEntityType.Event));
	}

	/// <inheritdoc/>
	public override async Task<PermissionStatus> RequestAsync()
	{
		EnsureDeclared();

		var status = EventPermission.CheckPermissionStatus(EKEntityType.Event);
		if (status == PermissionStatus.Granted)
		{
			return status;
		}

		var eventStore = new EKEventStore();

		bool granted;

		if (OperatingSystem.IsIOSVersionAtLeast(17, 0))
		{
			(granted, _) = await eventStore.RequestFullAccessToEventsAsync();
		}
		else
		{
			(granted, _) = await eventStore.RequestAccessAsync(EKEntityType.Event);
		}

		return granted ? PermissionStatus.Granted : PermissionStatus.Denied;
	}
}

public class RemindersCalendar : Permissions.BasePlatformPermission
{
	protected override Func<IEnumerable<string>> RequiredInfoPlistKeys =>
		() => new string[] { "NSRemindersFullAccessUsageDescription" };

	public override async Task<PermissionStatus> RequestAsync()
	{
		EnsureDeclared();

		var status = EventPermission.CheckPermissionStatus(EKEntityType.Event);
		if (status == PermissionStatus.Granted)
		{
			return status;
		}

		var eventStore = new EKEventStore();

		bool granted;

		if (OperatingSystem.IsIOSVersionAtLeast(17, 0))
		{
			(granted, _) = await eventStore.RequestFullAccessToRemindersAsync();
		}
		else
		{
			(granted, _) = await eventStore.RequestAccessAsync(EKEntityType.Reminder);
		}

		return granted ? PermissionStatus.Granted : PermissionStatus.Denied;
	}
}

static class EventPermission
{
	internal static PermissionStatus CheckPermissionStatus(EKEntityType entityType)
	{
		var status = EKEventStore.GetAuthorizationStatus(entityType);
		return status switch
		{
			EKAuthorizationStatus.Authorized => PermissionStatus.Granted,
			EKAuthorizationStatus.Denied => PermissionStatus.Denied,
			EKAuthorizationStatus.Restricted => PermissionStatus.Restricted,
			_ => PermissionStatus.Unknown,
		};
	}
}
