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
}
