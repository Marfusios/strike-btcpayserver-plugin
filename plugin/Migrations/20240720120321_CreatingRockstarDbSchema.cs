using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServerPlugins.RockstarDev.Strike.Migrations
{
    /// <inheritdoc />
    public partial class CreatingRockstarDbSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServerPlugins.RockstarDev.Strike");

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "BTCPayServerPlugins.RockstarDev.Strike",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LightningInvoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RealBtcFeeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    FeeCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ConversionRate = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "BTCPayServerPlugins.RockstarDev.Strike",
                columns: table => new
                {
                    InvoiceId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LightningInvoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PaymentHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RealBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConversionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false),
                    PaidConvertTo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.InvoiceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments",
                schema: "BTCPayServerPlugins.RockstarDev.Strike");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "BTCPayServerPlugins.RockstarDev.Strike");
        }
    }
}
