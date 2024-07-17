using System.Collections.Generic;

namespace BTCPayServer.Plugins.Strike;

/// <summary>
/// This class is a holder of Strike Lightning Clients by TenantId
/// </summary>
public class StrikeLightningClientLookup
{
	private readonly Dictionary<string, StrikeLightningClient> _clients = new();

	public StrikeLightningClient? GetClient(string tenantId)
	{
		if (_clients.TryGetValue(tenantId, out var client))
		{
			return client;
		}
		return null;
	}

	public void AddOrUpdateClient(string tenantId, StrikeLightningClient client)
	{
		_clients[tenantId] = client;
	}
}
