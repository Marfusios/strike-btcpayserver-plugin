using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeDbContextFactory : BaseDbContextFactory<StrikeDbContext>
{
	public static readonly string Schema = "BTCPayServerPlugins.RockstarDev.Strike";

	public StrikeDbContextFactory(IOptions<DatabaseOptions> options)
		: base(options, Schema)
	{
	}

	public override StrikeDbContext CreateContext()
	{
		var builder = new DbContextOptionsBuilder<StrikeDbContext>();
		ConfigureBuilder(builder);

		return new StrikeDbContext(builder.Options);
	}
}
