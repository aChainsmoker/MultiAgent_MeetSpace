namespace MeetSpace.Application.Exceptions;

public class AccessDeniedException : ApplicationException
{
    public AccessDeniedException(string message = "Access denied.")
        : base(message) { }
}
