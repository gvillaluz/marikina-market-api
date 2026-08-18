using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;

namespace MarikinaMarket.API.Application
{
    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException(string message) : base(message) { }
    }

    public class AccountLockedException : Exception
    {
        public AccountLockedException(string message) : base(message) { }
    }

    public class RecordNotFoundException : Exception
    {
        public RecordNotFoundException(string message) : base(message) { }
    }
    
    public class SessionExpiredException : Exception
    {
        public SessionExpiredException(string message) : base(message) { }
    }

    public class DuplicateOrdinanceException : Exception
    {
        public List<DuplicateOrdinance> DuplicateOrdinances { get; }

        public DuplicateOrdinanceException(string message, List<DuplicateOrdinance> duplicateOrdinances)
            : base(message)
        {
            DuplicateOrdinances = duplicateOrdinances;
        }
    }

    public class AlreadyProcessedException : Exception
    {
        public AlreadyProcessedException(string message) : base(message) { }
    }

    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(string message) : base(message) { }
    }

    public class ResourceCreationFailedException : Exception
    {
        public ResourceCreationFailedException(string message) : base(message) { }
    }

    public class InvalidRequestException : Exception
    {
        public InvalidRequestException(string message) : base(message) { }
    }

    public class DuplicateWarningException : Exception
    {
        public DuplicateWarningException(string message) : base(message) { }
    }
}
