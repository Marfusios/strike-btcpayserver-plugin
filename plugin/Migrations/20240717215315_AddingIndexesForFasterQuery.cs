using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
    /// <inheritdoc />
    public partial class AddingIndexesForFasterQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {            
	        migrationBuilder.Sql($"""
	                              CREATE INDEX idx_quotes_not_observed ON "BTCPayServer.Plugins.Strike"."Quotes"("TenantId") WHERE NOT("Observed");
	                              CREATE INDEX idx_quotes_paymenthash ON "BTCPayServer.Plugins.Strike"."Quotes"("PaymentHash");
	                              CREATE INDEX idx_payments_tenantid_paymenthash ON "BTCPayServer.Plugins.Strike"."Payments"("TenantId","PaymentHash");
	                              """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
	        migrationBuilder.Sql($"""
	                              DROP INDEX idx_quotes_not_observed;
	                              DROP INDEX idx_quotes_paymenthash;
	                              DROP INDEX idx_payments_tenantid_paymenthash;
	                              """);

        }
    }
}
