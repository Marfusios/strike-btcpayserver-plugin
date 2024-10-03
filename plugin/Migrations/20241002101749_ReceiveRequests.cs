using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
    /// <inheritdoc />
    public partial class ReceiveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "BTCPayServer.Plugins.Strike");

            migrationBuilder.CreateTable(
                name: "ReceiveRequests",
                schema: "BTCPayServer.Plugins.Strike",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiveRequestId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LightningInvoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PaymentHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PaymentPreimage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentCounterpartyId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RealBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConversionRate = table.Column<decimal>(type: "numeric", nullable: true),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiveRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveRequests_CreatedAt",
                schema: "BTCPayServer.Plugins.Strike",
                table: "ReceiveRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveRequests_PaidAt",
                schema: "BTCPayServer.Plugins.Strike",
                table: "ReceiveRequests",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveRequests_PaymentHash",
                schema: "BTCPayServer.Plugins.Strike",
                table: "ReceiveRequests",
                column: "PaymentHash");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveRequests_ReceiveRequestId",
                schema: "BTCPayServer.Plugins.Strike",
                table: "ReceiveRequests",
                column: "ReceiveRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveRequests_TenantId",
                schema: "BTCPayServer.Plugins.Strike",
                table: "ReceiveRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiveRequests",
                schema: "BTCPayServer.Plugins.Strike");

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    ConvertToCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Converted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvoiceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LightningInvoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RealBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RequestedBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });
        }
    }
}
