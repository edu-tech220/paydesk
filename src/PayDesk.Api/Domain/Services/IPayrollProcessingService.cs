using PayDesk.Api.Domain.Entities;

namespace PayDesk.Api.Domain.Services;

public interface IPayrollProcessingService
{
    /// <summary>
    /// Processes payroll for a closed pay period. Safe to call more than once for the
    /// same period: if a run already exists it is returned as-is instead of being
    /// recomputed or duplicated.
    /// </summary>
    Task<PayrollRun> ProcessAsync(int payPeriodId, CancellationToken cancellationToken = default);
}
