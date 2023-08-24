namespace Plugin.Maui.CalendarStore.Sample;

public partial class EventsPage : ContentPage
{
	public CalendarEvent[] Events { get; set; }

	public EventsPage()
	{
		InitializeComponent();
	}

	async void Button_Clicked_Events(object sender, EventArgs e)
	{
		Events = (await CalendarStore.Default.GetEvents(startDate: DateTimeOffset.Now.AddDays(-7),
			endDate: DateTimeOffset.Now.AddDays(7))).ToArray();

		BindingContext = this;
	}
}