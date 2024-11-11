using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeDbContextFactory : BaseDbContextFactory<StrikeDbContext>
{
	public static readonly string Schema = "BTCPayServer.Plugins.Strike";

	public StrikeDbContextFactory(IOptions<DatabaseOptions> options)
		: base(options, Schema)
	{
	}

	public override StrikeDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
	{
		var builder = new DbContextOptionsBuilder<StrikeDbContext>();
		ConfigureBuilder(builder, npgsqlOptionsAction);

		return new StrikeDbContext(builder.Options);
	}
}
