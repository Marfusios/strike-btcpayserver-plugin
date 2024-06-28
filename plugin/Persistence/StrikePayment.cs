using System;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikePayment : IHasTenantId
{
	public long Id { get; init; }

	public string TenantId { get; set; } = string.Empty;

	public string PaymentId { get; init; } = string.Empty;

	public string? LightningInvoice { get; init; } = string.Empty;

	public string PaymentHash { get; init; } = string.Empty;

	public DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// BTC amount requested by BTCPayServer
	/// </summary>
	public decimal RequestedBtcAmount { get; init; }

	/// <summary>
	/// Target amount to be sent from Strike, can be fiat or BTC
	/// </summary>
	public decimal TargetAmount { get; init; }

	/// <summary>
	/// Target currency to be sent from Strike, can be fiat or BTC
	/// </summary>
	public string TargetCurrency { get; init; } = string.Empty;

	/// <summary>
	/// Real taken fee amount in BTC
	/// </summary>
	public decimal RealBtcFeeAmount { get; set; }

	/// <summary>
	/// Real taken fee amount, can be fiat or BTC
	/// </summary>
	public decimal? FeeAmount { get; init; }

	/// <summary>
	/// Real taken fee currency, can be fiat or BTC
	/// </summary>
	public string? FeeCurrency { get; init; } = string.Empty;

	/// <summary>
	/// Conversion rate in case the target amount is in fiat
	/// </summary>
	public decimal? ConversionRate { get; init; }

	/// <summary>
	/// Recently observed status
	/// </summary>
	public LightningPaymentStatus Status { get; set; }
}

public class StrikePaymentConfiguration : IEntityTypeConfiguration<StrikePayment>
{
	public void Configure(EntityTypeBuilder<StrikePayment> builder)
	{
		builder.ToTable("Payments");
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.TenantId).HasMaxLength(300);
		builder.Property(x => x.PaymentId).HasMaxLength(300);
		builder.Property(x => x.LightningInvoice).HasMaxLength(1000);
		builder.Property(x => x.PaymentHash).HasMaxLength(300);

		builder.Property(x => x.RequestedBtcAmount);
		builder.Property(x => x.TargetAmount);
		builder.Property(x => x.TargetCurrency).HasMaxLength(10);
		builder.Property(x => x.RealBtcFeeAmount);
		builder.Property(x => x.FeeAmount);
		builder.Property(x => x.FeeCurrency).HasMaxLength(10);
		builder.Property(x => x.ConversionRate);

		builder.Property(x => x.CreatedAt);
		builder.Property(x => x.CompletedAt);
		builder.Property(x => x.Status);
	}
}

