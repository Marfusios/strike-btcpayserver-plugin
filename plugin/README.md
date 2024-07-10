# Strike plugin

Allows to use a [Strike Wallet](https://strike.me) account as the lightning provider for BTCPay Server.

### Adding database migrations

dotnet ef migrations add AddingPaidConvertTo -p plugin -c StrikeDbContext -o Migrations
