namespace Plugin.Maui.Calendar.Sample;

public partial class MainPage : ContentPage
{
	readonly ICalendars calendars;

	public Calendar[] Calendars { get; set; }
	public CalendarEvent[] Events { get; set; }

	public MainPage(ICalendars calendars)
	{
		InitializeComponent();
		
		this.calendars = calendars;
	}

	async void Button_Clicked_Calendars(object sender, EventArgs e)
	{
		Calendars = (await calendars.GetCalendarsAsync()).ToArray();

		BindingContext = this;
	}

	async void Button_Clicked_Events(object sender, EventArgs e)
	{
		Events = (await calendars.GetEventsAsync(startDate: DateTimeOffset.Now.AddDays(-1),
			endDate: DateTimeOffset.Now.AddDays(1))).ToArray();

		BindingContext = this;
	}
}
