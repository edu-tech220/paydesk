namespace PayDesk.Api.Domain.Entities;

/// <summary>
/// Implemented by entities whose Version column PayDeskDbContext increments on every
/// save, so callers can submit the Version they last read and get a 409/concurrency
/// failure if someone else updated the row first.
/// </summary>
public interface IConcurrencyVersioned
{
    int Version { get; set; }
}
