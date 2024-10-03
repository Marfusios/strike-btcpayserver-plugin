using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeReceiveRequest : IHasTenantId
{
	public long Id { get; init; }

	public string ReceiveRequestId { get; init; } = string.Empty;

	public string TenantId { get; set; } = string.Empty;

	public string LightningInvoice { get; init; } = string.Empty;

	public string PaymentHash { get; init; } = string.Empty;
	public string? PaymentPreimage { get; set; }
	public string? PaymentCounterpartyId { get; set; }

	public string? Description { get; init; }

	public DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset ExpiresAt { get; init; }

	public DateTimeOffset? PaidAt { get; set; }

	/// <summary>
	/// BTC amount requested by BTCPayServer
	/// </summary>
	public decimal RequestedBtcAmount { get; init; }

	/// <summary>
	/// Real BTC amount calculated by Strike
	/// </summary>
	public decimal RealBtcAmount { get; init; }

	/// <summary>
	/// Target amount to be received on Strike, can be fiat or BTC
	/// </summary>
	public decimal? TargetAmount { get; set; }

	/// <summary>
	/// Target currency to be received on Strike, can be fiat or BTC
	/// </summary>
	public string TargetCurrency { get; set; } = string.Empty;

	/// <summary>
	/// Conversion rate in case the target amount is in fiat
	/// </summary>
	public decimal? ConversionRate { get; set; }

	public bool Paid { get; set; }

	public bool Observed { get; set; }

	public bool IsExpired => !Paid && ExpiresAt < DateTimeOffset.UtcNow;
}

public class StrikeReceiveRequestConfiguration : IEntityTypeConfiguration<StrikeReceiveRequest>
{
	public void Configure(EntityTypeBuilder<StrikeReceiveRequest> builder)
	{
		builder.ToTable("ReceiveRequests");
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.TenantId).HasMaxLength(300);
		builder.Property(x => x.ReceiveRequestId).HasMaxLength(300);
		builder.Property(x => x.LightningInvoice).HasMaxLength(1000);
		builder.Property(x => x.PaymentHash).HasMaxLength(300);
		builder.Property(x => x.PaymentPreimage).HasMaxLength(1000);
		builder.Property(x => x.PaymentCounterpartyId).HasMaxLength(300);
		builder.Property(x => x.Description).HasMaxLength(1000);

		builder.Property(x => x.RequestedBtcAmount);
		builder.Property(x => x.RealBtcAmount);
		builder.Property(x => x.TargetAmount);
		builder.Property(x => x.TargetCurrency).HasMaxLength(10);
		builder.Property(x => x.ConversionRate);

		builder.Property(x => x.CreatedAt);
		builder.Property(x => x.ExpiresAt);
		builder.Property(x => x.PaidAt);
		builder.Property(x => x.Paid);
		builder.Property(x => x.Observed);

		builder.HasIndex(x => x.TenantId);
		builder.HasIndex(x => x.ReceiveRequestId);
		builder.HasIndex(x => x.PaymentHash);
		builder.HasIndex(x => x.CreatedAt);
		builder.HasIndex(x => x.PaidAt);
	}
}
