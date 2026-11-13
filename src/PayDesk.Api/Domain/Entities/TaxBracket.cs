namespace PayDesk.Api.Domain.Entities;

/// <summary>
/// A slice of a progressive tax table: income between [MinAmount, MaxAmount) in this
/// bracket is taxed at Rate. MaxAmount is null for the top, uncapped bracket.
/// </summary>
public class TaxBracket
{
    public int Id { get; set; }
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal Rate { get; set; }
}
