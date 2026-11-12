namespace Appointment.Server.Models
{
    public class BookingOptions
    {
        public const string SectionName = "Booking";

        public TimeSpan OpeningTime { get; set; } = new(9, 0, 0);
        public TimeSpan ClosingTime { get; set; } = new(17, 0, 0);
        public int SlotIntervalMinutes { get; set; } = 30;
        public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    }
}
