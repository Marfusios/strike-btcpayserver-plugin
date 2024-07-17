using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.Strike.Migrations
{
    /// <inheritdoc />
    public partial class ConvertToCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConvertToCurrency",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Converted",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvertToCurrency",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Converted",
                schema: "BTCPayServer.Plugins.Strike",
                table: "Quotes");
        }
    }
}
