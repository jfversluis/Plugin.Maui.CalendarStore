namespace Plugin.Maui.CalendarStore.Sample;

public partial class AddEventsPage : ContentPage
{
	readonly ICalendarStore calendarStore;

	public AddEventsPage(ICalendarStore calendarStore)
	{
		InitializeComponent();

		this.calendarStore = calendarStore;
	}

	async void Button_Clicked(object sender, EventArgs e)
	{

		try
		{
			var cal = await calendarStore.GetCalendars();

			await calendarStore.CreateEvent(cal.Skip(1).First().Id, "Test", "Test",
				DateTimeOffset.Now, DateTimeOffset.Now.AddHours(1));

			//await calendarStore.CreateAllDayEvent(cal.Skip(1).First().Id, "Test",
			//	"Test", DateTimeOffset.Now.AddDays(1), DateTimeOffset.Now.AddDays(3));
		}
		catch(Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
}
