using System.Linq;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Strike;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
[Route("plugins/{storeId}/strike")]
public class StrikeController : Controller
{
    private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
    private readonly BTCPayWalletProvider _btcWalletProvider;
    private readonly StoreRepository _storeRepository;

    public StrikeController(BTCPayNetworkProvider btcPayNetworkProvider,
        BTCPayWalletProvider btcWalletProvider, StoreRepository storeRepository)
    {
        _btcPayNetworkProvider = btcPayNetworkProvider;
        _btcWalletProvider = btcWalletProvider;
        _storeRepository = storeRepository;
    }


    [HttpGet("")]
    public IActionResult Index(string storeId)
    {
        return RedirectToAction(nameof(Dashboard), new {storeId});
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public IActionResult Dashboard(string storeId)
    {
	    return View((object)storeId);
    }
    
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpGet("configure")]
    public IActionResult Configure(string storeId)
    {
        return View();
    }

    [HttpPost("configure")]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public IActionResult Configure(string storeId, string command, string settings)
    {
        var store = HttpContext.GetStoreData();
        var existing = store.GetSupportedPaymentMethods(_btcPayNetworkProvider).OfType<LightningSupportedPaymentMethod>()
            .FirstOrDefault(method =>
                method.PaymentId.PaymentType == LightningPaymentType.Instance &&
                method.PaymentId.CryptoCode == "BTC");

        return RedirectToAction(nameof(Configure), new {storeId});
    }
}
