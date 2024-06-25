using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Strike.Client;

namespace BTCPayServer.Plugins.Strike;

public class StrikePlugin : BaseBTCPayServerPlugin
{
	public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
	{
		new() {Identifier = nameof(BTCPayServer), Condition = ">=1.12.0"}

	};

	public override void Execute(IServiceCollection applicationBuilder)
	{
		applicationBuilder.AddSingleton<IUIExtension>(new UIExtension("Strike/LNPaymentMethodSetupTab", "ln-payment-method-setup-tab"));
		applicationBuilder.AddSingleton<ILightningConnectionStringHandler>(provider => provider.GetRequiredService<StrikeLightningConnectionStringHandler>());
		applicationBuilder.AddSingleton<StrikeLightningConnectionStringHandler>();

		applicationBuilder.AddSingleton<StrikeDbContextFactory>();
		applicationBuilder.AddDbContext<StrikeDbContext>((provider, o) =>
		{
			var factory = provider.GetRequiredService<StrikeDbContextFactory>();
			factory.ConfigureBuilder(o);
		});

		applicationBuilder.AddSingleton<StrikeStorageFactory>();
		applicationBuilder.AddTransient<StrikeStorage>();

		applicationBuilder.AddStrikeHttpClient();
		applicationBuilder.AddStrikeClient();

		base.Execute(applicationBuilder);
	}

}
