using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
    /// <inheritdoc />
    public partial class DroppingQuoteIdMovingToInvoiceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceId",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceId",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quotes",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                column: "Id");
        }
    }
}
