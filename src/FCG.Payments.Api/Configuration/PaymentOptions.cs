using System.ComponentModel.DataAnnotations;

namespace FCG.Payments.Api.Configuration;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal ApprovalLimit { get; init; } = 100.00m;
}
