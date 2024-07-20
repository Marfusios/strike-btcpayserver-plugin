# Strike BTCPayServer Plugin, Rockstar'd version

This plugin allows you to use your [Strike](https://strike.me) account as the lightning provider for the BTCPay Server.

It is based on [Marfusios Strike Plugin](https://github.com/Marfusios/strike-btcpayserver-plugin/), optimized and tested to work with a large number of stores.

### License: 
MIT

### Features

- Receive lightning payments directly to Strike Wallet in BTC
- BTC price hedge - convert into fiat (USD|EUR) on invoice paid
- Lightning Network liquidity managed by Strike

### Usage

- Visit [Strike Dashboard](https://dashboard.strike.me/login) and obtain an API key. Select all scopes under **Account**, **Receiving payments**, and **Rates**.

> :warning: **Owner of this BTCPayServer instance can access your API key.** 
> Therefore they could spend your Strike balance if **Sending payments** scopes are selected.

![ApiKey](docs/api_key_dark.png)

- Install `Strike Rockstar'd` plugin from the `Manage Plugins` page (or ask a BTCPayServer admin).
- Go to `BTCPayServer > Lightning > Settings > "Change connection" > "Use custom node"` and configure Strike connection. Follow this format: 

    ```
    type=strike;convert-to=USD;api-key=xxx
    ```

    Where xxx is your API key. 
    Set `convert-to` parameter to USD or EUR if you want conversion after the invoice is paid. This parameter is optional, and if not included it'll be set to UNDEFINED.

- If you are converting to fiat, ensure enough spread for conversion and set invoice expiry:
    - General -> Invoice expires in 2 minutes
    - Rates -> Add Exchange Rate Spread -> 1%

### Feature Requests and Pull Requests

For now, please refrain from opening PRs on this repository until changes are merged into Marfusios source repo. Feel free to open an issue for discussions or DM me on Nostr, Twitter, or Telegram if you want to talk about improvements.