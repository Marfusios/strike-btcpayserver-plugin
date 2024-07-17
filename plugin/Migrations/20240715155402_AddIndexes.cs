using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
	[DbContext(typeof(StrikeDbContext))]
	[Migration("20240715155402_AddIndexes")]
	public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("""
ALTER TABLE "BTCPayServer.Plugins.Strike"."Quotes" DROP CONSTRAINT "PK_Quotes";
ALTER TABLE "BTCPayServer.Plugins.Strike"."Quotes" DROP COLUMN "Id";
ALTER TABLE "BTCPayServer.Plugins.Strike"."Quotes" ADD CONSTRAINT "PK_Quotes" PRIMARY KEY("InvoiceId");
CREATE INDEX idx_quotes_not_observed ON "BTCPayServer.Plugins.Strike"."Quotes"("TenantId") WHERE NOT("Observed");
CREATE INDEX idx_quotes_paymenthash ON "BTCPayServer.Plugins.Strike"."Quotes"("PaymentHash");
CREATE INDEX idx_payments_tenantid_paymenthash ON "BTCPayServer.Plugins.Strike"."Payments"("TenantId","PaymentHash");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
