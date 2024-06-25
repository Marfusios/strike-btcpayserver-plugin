using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeDbContextDesignFactory : IDesignTimeDbContextFactory<StrikeDbContext>
{
	public StrikeDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<StrikeDbContext>();

		builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=btcpayserver4");
		return new StrikeDbContext(builder.Options);
	}
}
