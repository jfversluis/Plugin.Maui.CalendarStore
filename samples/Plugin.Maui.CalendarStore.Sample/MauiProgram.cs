﻿namespace Plugin.Maui.CalendarStore.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddTransient<CalendarsPage>();
		builder.Services.AddTransient<AddEventsPage>();
		builder.Services.AddSingleton<ICalendarStore>(CalendarStore.Default);

		return builder.Build();
	}
}