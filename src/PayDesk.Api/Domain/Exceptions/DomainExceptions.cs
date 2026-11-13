namespace PayDesk.Api.Domain.Exceptions;

/// <summary>Requested aggregate (employee, pay period, timesheet entry...) does not exist.</summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>A business rule was violated (e.g. closing a period with pending approvals).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>A concurrent write beat this request to the same aggregate.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
