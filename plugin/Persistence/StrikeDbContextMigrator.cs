using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Strike.Persistence;

internal class StrikeDbContextMigrator : IHostedService
{
	private readonly StrikeDbContextFactory _dbContextFactory;
	private readonly ILogger<StrikeDbContextMigrator> _logger;

	public StrikeDbContextMigrator(StrikeDbContextFactory dbContextFactory, ILogger<StrikeDbContextMigrator> logger)
	{
		_dbContextFactory = dbContextFactory;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogDebug("Migrating Strike database");
			await using var ctx = _dbContextFactory.CreateContext();
			await using var dbContext = _dbContextFactory.CreateContext();
			await ctx.Database.MigrateAsync(cancellationToken);
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Error while migrating Strike database: {error}", e.Message);
		}
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
