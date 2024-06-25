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
        
        // if (command == "clear")
        // {
        //     await _breezService.Set(storeId, null);
        //     TempData[WellKnownTempData.SuccessMessage] = "Settings cleared successfully";
        //     var client = _breezService.GetClient(storeId);
        //     var isStoreSetToThisMicro = existing?.GetExternalLightningUrl() == client?.ToString();
        //     if (client is not null && isStoreSetToThisMicro)
        //     {
        //         store.SetSupportedPaymentMethod(existing.PaymentId, null);
        //         await _storeRepository.UpdateStore(store);
        //     }
        //     return RedirectToAction(nameof(Configure), new {storeId});
        // }
        //
        // if (command == "save")
        // {
        //   
        //     try
        //     {
        //         if (string.IsNullOrEmpty(settings.Mnemonic))
        //         {
        //             ModelState.AddModelError(nameof(settings.Mnemonic), "Mnemonic is required");
        //             return View(settings);
        //         }
        //         else
        //          {
        //             try
        //             {
        //                 new Mnemonic(settings.Mnemonic);
        //             }
        //             catch (Exception e)
        //             {
        //                 ModelState.AddModelError(nameof(settings.Mnemonic), "Invalid mnemonic");
        //                 return View(settings);
        //             }
        //         }
        //
        //         if (settings.GreenlightCredentials is not null)
        //         {
        //             await using var stream = settings.GreenlightCredentials .OpenReadStream();
        //             using var archive = new ZipArchive(stream);
        //             var deviceClientArchiveEntry = archive.GetEntry("client.crt");
        //             var deviceKeyArchiveEntry = archive.GetEntry("client-key.pem");
        //             if(deviceClientArchiveEntry is null || deviceKeyArchiveEntry is null)
        //             {
        //                ModelState.AddModelError(nameof(settings.GreenlightCredentials), "Invalid zip file (does not have client.crt or client-key.pem)");
        //                return View(settings);
        //             }
        //             else
        //             {
        //                 var deviceClient = await ReadAsByteArrayAsync(deviceClientArchiveEntry.Open());
        //                 var deviceKey = await ReadAsByteArrayAsync(deviceKeyArchiveEntry.Open());
        //                 var dir = _breezService.GetWorkDir(storeId);
        //                 Directory.CreateDirectory(dir);
        //                 await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, "client.crt"), deviceClient);
        //                 await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, "client-key.pem"), deviceKey);
        //                 
        //                 await _breezService.Set(storeId, settings);
        //             }
        //             
        //         }
        //         else
        //         {
        //             
        //             await _breezService.Set(storeId, settings);
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         TempData[WellKnownTempData.ErrorMessage] = $"Couldnt use provided settings: {e.Message}";
        //         return View(settings);
        //     }
        //
        //     if(existing is null)
        //     {
        //         existing = new LightningSupportedPaymentMethod()
        //         {
        //             CryptoCode = "BTC"
        //         };
        //         var client = _breezService.GetClient(storeId);
        //         existing.SetLightningUrl(client);
        //         store.SetSupportedPaymentMethod(existing);
        //         var lnurl = new LNURLPaySupportedPaymentMethod()
        //         {
        //             CryptoCode = "BTC",
        //         };
        //         store.SetSupportedPaymentMethod(lnurl);
        //         await _storeRepository.UpdateStore(store);
        //     }
        //     
        //     TempData[WellKnownTempData.SuccessMessage] = "Settings saved successfully";
        //     return RedirectToAction(nameof(Info), new {storeId});
        // }
        //return NotFound();
        
        return RedirectToAction(nameof(Configure), new {storeId});
    }
}
