using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Appointment.Server.Models
{
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
                   v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
    }

    public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
                   v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
    }
}
