using System.ComponentModel.DataAnnotations;

namespace PayDesk.Api.Contracts.PayPeriods;

public class PayPeriodResponse
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class CreatePayPeriodRequest
{
    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }
}

public class VersionedActionRequest
{
    [Required]
    public int ExpectedVersion { get; set; }
}
