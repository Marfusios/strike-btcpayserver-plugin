# Strike plugin

Allows to use a [Strike Wallet](https://strike.me) account as the lightning provider for BTCPay Server.

### Adding database migrations

- Comment out `ItemDefinitionGroup` in BTCPayServer.Plugins.Strike.csproj
- Execute command in the main directory: `dotnet ef migrations add MyMigration -p plugin -c StrikeDbContext -o Migrations`
- Uncomment `ItemDefinitionGroup` in BTCPayServer.Plugins.Strike.csproj
