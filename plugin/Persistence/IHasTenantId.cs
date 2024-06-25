namespace BTCPayServer.Plugins.Strike.Persistence;
public interface IHasTenantId
{
	public string TenantId { get; internal set; }
}
