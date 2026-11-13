using PayDesk.Api.Domain.Entities;

namespace PayDesk.Api.Domain.Services;

public interface IProgressiveTaxCalculator
{
    decimal CalculateTax(decimal grossPay, IEnumerable<TaxBracket> brackets);
}
