namespace Appointment.Server.Models
{
    /// <summary>Opening hours and slot grid. Times are wall-clock values expressed in UTC.</summary>
    public class BookingOptions
    {
        public const string SectionName = "Booking";

        public TimeSpan OpeningTime { get; set; } = new(9, 0, 0);
        public TimeSpan ClosingTime { get; set; } = new(17, 0, 0);
        public int SlotIntervalMinutes { get; set; } = 30;
    }
}
