using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeStorageFactory
{
	private readonly StrikeDbContextFactory _dbFactory;
	private readonly ILogger<StrikeStorageFactory> _logger;

	public string? TenantId { get; set; }

	public StrikeStorageFactory(StrikeDbContextFactory dbFactory, ILogger<StrikeStorageFactory> logger)
	{
		_dbFactory = dbFactory;
		_logger = logger;
	}

	public StrikeStorage ResolveStorage()
	{
		return new StrikeStorage(_dbFactory.CreateContext(), _logger)
		{
			TenantId = TenantId
		};
	}
}
