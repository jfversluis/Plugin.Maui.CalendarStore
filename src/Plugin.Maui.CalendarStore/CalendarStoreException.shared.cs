namespace Plugin.Maui.CalendarStore;

public static partial class CalendarStore
{
	public class CalendarStoreException : Exception
	{
		public CalendarStoreException(string message)
			: base(message) { }
	}
}
