namespace Plugin.Maui.CalendarStore.Sample;

public partial class CalendarsPage : ContentPage
{
	readonly ICalendars calendars;

	public Calendar[] Calendars { get; set; }

	public CalendarsPage(ICalendars calendars)
	{
		InitializeComponent();
		
		this.calendars = calendars;
	}

	async void Button_Clicked_Calendars(object sender, EventArgs e)
	{
		Calendars = (await calendars.GetCalendars()).ToArray();

		BindingContext = this;
	}
}
