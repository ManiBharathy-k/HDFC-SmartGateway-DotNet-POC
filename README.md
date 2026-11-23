# HDFC SmartGateway – .NET Sandbox Integration

A single-file ASP.NET Core integration adapter for sending **real HTTP requests to the HDFC SmartGateway sandbox** using HTTP Basic Authentication.

> **Sandbox only.** This project no longer contains a local mock checkout or simulated payment completion. It forwards the JSON request you provide to the configured HDFC sandbox endpoint and returns HDFC's HTTP response. It does not invent HDFC request fields, endpoint paths, or callback verification rules.

## Requirements

- .NET 8 SDK or later
- HDFC SmartGateway sandbox access
- The merchant's **SmartGateway API Reference (Basic Authentication)** and issued sandbox credentials
- Postman or curl

## Configure environment

Copy `.env.example` to `.env` and fill in the values supplied by HDFC. Never commit `.env`.

The exact sandbox base URL, endpoint paths, Basic Auth username format, optional merchant header, and JSON body must come from the merchant's HDFC onboarding pack/API reference. Do not use production URLs or credentials for sandbox testing.

| Variable | Purpose |
|---|---|
| `HDFC_BASE_URL` | Official sandbox API base URL |
| `HDFC_BASIC_AUTH_USERNAME` | Exact Basic Auth username issued/documented by HDFC |
| `HDFC_BASIC_AUTH_PASSWORD` | Sandbox API password |
| `HDFC_MERCHANT_ID` | Merchant ID, if required as a separate header |
| `HDFC_MERCHANT_HEADER_NAME` | Exact header name, only if required by the selected API |
| `HDFC_CREATE_ORDER_PATH` | Exact create-order API path |
| `HDFC_ORDER_STATUS_PATH_TEMPLATE` | Exact status API path, using `{orderId}` where the order ID belongs |
| `HDFC_TIMEOUT_SECONDS` | Optional HTTP timeout (default 30 seconds) |

These are application environment variable names. They are not claims about HDFC's required header names or request schema.

### Load the .env file

The .NET runtime does not load `.env` files automatically. On PowerShell, load the file into the current terminal session:

```powershell
Get-Content .env | Where-Object { $_ -match '^\s*[^#].+=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim().Trim('"'), 'Process')
}
dotnet run --urls http://localhost:5000
```

On bash/zsh:

```bash
set -a
source .env
set +a
dotnet run --urls http://localhost:5000
```

## Run

```bash
dotnet new web -n HDFCSmartGatewayPOC
cd HDFCSmartGatewayPOC
```

Replace the generated `Program.cs` with this repository's `Program.cs`, configure environment variables, and run:

```bash
dotnet run --urls http://localhost:5000
```

## API endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/health` | Local application health |
| POST | `/api/payments/create` | Forwards JSON to the configured HDFC sandbox create-order endpoint |
| GET | `/api/payments/status/{orderId}` | Forwards to the configured HDFC sandbox status endpoint |

## Create an HDFC sandbox order

**POST** `http://localhost:5000/api/payments/create`

Send the exact JSON request body specified in the merchant's HDFC Basic Auth API reference. Example structure only:

```json
{
  "REPLACE_WITH_FIELDS_FROM_HDFC_DOCUMENTATION": "Use the exact documented request schema"
}
```

The service sends that JSON to `HDFC_BASE_URL + HDFC_CREATE_ORDER_PATH` using HTTP Basic Authentication. It returns HDFC's response body and HTTP status code to the caller.

## Check payment status

**GET** `http://localhost:5000/api/payments/status/{orderId}`

The application replaces `{orderId}` in `HDFC_ORDER_STATUS_PATH_TEMPLATE` with the URL-encoded order ID and calls the configured HDFC sandbox API.

## Checkout, return URL, and webhooks

This API adapter deliberately does not fabricate a checkout URL or callback contract. Implement the browser redirect using the exact checkout/redirect field returned by HDFC's create-order response and its Basic Auth documentation.

A browser return/redirect is not proof of payment. Confirm the final transaction state using HDFC's documented server-side status API.

Configure the public HTTPS return URL and webhook/notification URL with HDFC only where required by the merchant integration. Callback authentication, signature verification, event schema, and retry/idempotency behavior must be implemented from the exact HDFC documentation before accepting callbacks. No unverified callback endpoint is enabled in this starter.

## Security notes

- Keep `.env` out of Git. The repository ignores it.
- Never commit merchant credentials, API passwords, signing keys, or certificates.
- Use sandbox credentials only during sandbox testing.
- Do not expose this backend's gateway proxy as an unrestricted public proxy. Add application authentication, authorization, rate limiting, request validation, and audit logging before deployment.
- Do not log Authorization headers or secrets.
- Use HTTPS for public deployments.
- Validate payment amount/order ownership on your server; do not trust client-submitted payment status.

## Official documentation

- [HDFC SmartGateway Developer Docs](https://smartgateway.hdfc.bank.in/docs/)
- [SmartGateway API Reference – Basic Authentication](https://smartgateway.hdfc.bank.in/docs/smartgateway-api-ref-basicauth/docs/overview/integration-architecture)

Use the merchant's current API reference/onboarding details as the authority for the exact endpoint URLs, request/response fields, authentication format, checkout flow, and callback behavior.

## Current scope

This is a real sandbox HTTP adapter, not a finished production payment integration. It makes outbound authenticated calls when correctly configured. The actual request schema, checkout redirect handling, transaction state mapping, and verified webhook processing must be completed against the merchant's exact HDFC Basic Auth API contract.

## License

MIT. See [LICENSE](LICENSE).
