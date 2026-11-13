using PayDesk.Api.Domain.Entities;

namespace PayDesk.Api.Domain.Services;

/// <summary>
/// Applies a progressive (marginal) tax table: only the slice of gross pay that
/// falls inside a bracket is taxed at that bracket's rate, not the whole amount.
/// </summary>
public class ProgressiveTaxCalculator : IProgressiveTaxCalculator
{
    public decimal CalculateTax(decimal grossPay, IEnumerable<TaxBracket> brackets)
    {
        if (grossPay <= 0m)
        {
            return 0m;
        }

        decimal tax = 0m;

        foreach (var bracket in brackets.OrderBy(b => b.MinAmount))
        {
            if (grossPay <= bracket.MinAmount)
            {
                continue;
            }

            var upperBound = bracket.MaxAmount.HasValue ? Math.Min(grossPay, bracket.MaxAmount.Value) : grossPay;
            var taxableSlice = upperBound - bracket.MinAmount;

            if (taxableSlice > 0m)
            {
                tax += taxableSlice * bracket.Rate;
            }
        }

        return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
    }
}
