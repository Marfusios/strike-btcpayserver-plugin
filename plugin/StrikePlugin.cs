using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Strike.Client;

namespace BTCPayServer.Plugins.Strike;

public class StrikePlugin : BaseBTCPayServerPlugin
{
	public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
	{
		new() {Identifier = nameof(BTCPayServer), Condition = ">=2.0.0"}

	};

	public override void Execute(IServiceCollection applicationBuilder)
	{
		applicationBuilder.AddUIExtension("ln-payment-method-setup-tab", "Strike/LNPaymentMethodSetupTab");
		applicationBuilder.AddUIExtension("store-integrations-nav", "Strike/StrikeNav");
		applicationBuilder.AddUIExtension("dashboard", "Strike/StrikeBalances");

		applicationBuilder.AddSingleton<ILightningConnectionStringHandler>(provider => provider.GetRequiredService<StrikeLightningConnectionStringHandler>());
		applicationBuilder.AddSingleton<StrikeLightningConnectionStringHandler>();
		applicationBuilder.AddSingleton<StrikeLightningClientLookup>();

		applicationBuilder.AddSingleton<StrikeDbContextFactory>();
		applicationBuilder.AddDbContext<StrikeDbContext>((provider, o) =>
		{
			var factory = provider.GetRequiredService<StrikeDbContextFactory>();
			factory.ConfigureBuilder(o);
		});
		applicationBuilder.AddHostedService<StrikeDbContextMigrator>();

		applicationBuilder.AddTransient<StrikeStorageFactory>();
		applicationBuilder.AddTransient<StrikeStorage>();
		applicationBuilder.AddTransient<StrikeClientFinder>();

		applicationBuilder.AddStrikeHttpClient();
		applicationBuilder.AddStrikeClient();

		base.Execute(applicationBuilder);
	}

}
