namespace Appointment.Server.Exceptions
{
    public abstract class AppException : Exception
    {
        protected AppException(string message) : base(message) { }
        public abstract int StatusCode { get; }
        public abstract string Title { get; }
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string message) : base(message) { }
        public override int StatusCode => StatusCodes.Status404NotFound;
        public override string Title => "Resource not found";
    }

    public class BusinessRuleException : AppException
    {
        public BusinessRuleException(string message) : base(message) { }
        public override int StatusCode => StatusCodes.Status400BadRequest;
        public override string Title => "Business rule violation";
    }

    public class SlotUnavailableException : AppException
    {
        public SlotUnavailableException(string message) : base(message) { }
        public override int StatusCode => StatusCodes.Status409Conflict;
        public override string Title => "Time slot unavailable";
    }
}
