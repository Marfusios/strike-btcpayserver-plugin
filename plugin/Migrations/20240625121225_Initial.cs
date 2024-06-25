using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.Strike");

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    InvoiceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LightningInvoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PaymentHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RealBtcAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TargetCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConversionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "BTCPayServer.Plugins.Strike");
        }
    }
}
