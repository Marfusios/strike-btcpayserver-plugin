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
CREATE INDEX idx_quotes_not_observed ON "BTCPayServer.Plugins.Strike"."Quotes"("TenantId") WHERE NOT("Observed");
CREATE INDEX idx_quotes_paymenthash ON "BTCPayServer.Plugins.Strike"."Quotes"("PaymentHash");
CREATE INDEX idx_quotes_invoiceid ON "BTCPayServer.Plugins.Strike"."Quotes"("InvoiceId");
CREATE INDEX idx_payments_tenantid_paymenthash ON "BTCPayServer.Plugins.Strike"."Payments"("TenantId","PaymentHash");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
