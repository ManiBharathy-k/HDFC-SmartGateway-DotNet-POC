# HDFC SmartGateway – .NET 8 Sandbox Integration

A real server-to-server integration with the HDFC SmartGateway UAT environment. It creates HDFC payment sessions, returns the HDFC-hosted checkout link, fetches authoritative order status, and verifies signed browser-return parameters.

**Environment:** HDFC UAT only. The app refuses a non-UAT host. No local mock checkout or fake payment-success endpoint is included.

## HDFC configuration supplied

The repository uses these non-secret sandbox defaults:

- Base URL: \`https://smartgateway.hdfcuat.bank.in\`
- Merchant ID: \`SG6695\`
- Payment page client ID: \`hdfcmaster\`
- Logging: \`false\`

The API key was blank in the configuration shared with us. The response key was masked as \`******CF2C\`. Enter both actual values locally in \`.env\`; never commit them.

## Requirements

- .NET 8 SDK
- HDFC SmartGateway UAT merchant access
- API key and response key from the HDFC/Juspay merchant dashboard
- A public HTTPS return URL on your application (localhost is not reachable by HDFC servers)

## Setup

Clone:

\`\`\`bash
git clone https://github.com/ManiBharathy-k/HDFC-SmartGateway-DotNet-POC.git
cd HDFC-SmartGateway-DotNet-POC
\`\`\`

Copy \`.env.example\` to \`.env\` and fill in the API key, response key, and return URL.

### PowerShell

\`\`\`powershell
Copy-Item .env.example .env
Get-Content .env | Where-Object { $_ -match '^\s*[^#].+=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim().Trim('"'), 'Process')
}
dotnet run --urls http://localhost:5000
\`\`\`

### Bash

\`\`\`bash
cp .env.example .env
set -a
source .env
set +a
dotnet run --urls http://localhost:5000
\`\`\`

The .NET app does not automatically read \`.env\`; use one of the loading methods above or configure equivalent environment variables in your hosting platform.

## Environment variables

| Variable | Meaning |
|---|---|
| \`HDFC_BASE_URL\` | UAT API base URL; defaults to the supplied HDFC UAT host |
| \`HDFC_MERCHANT_ID\` | Merchant ID; defaults to \`SG6695\` |
| \`HDFC_API_KEY\` | API key used as the Basic Auth username; required |
| \`HDFC_PAYMENT_PAGE_CLIENT_ID\` | \`hdfcmaster\` for UAT |
| \`HDFC_RESPONSE_KEY\` | Response key used for signed return verification; required to verify returns |
| \`HDFC_RETURN_URL\` | Example value is in \`.env.example\`; use the same URL in the API request body |
| \`HDFC_API_VERSION\` | API version header; defaults to \`2024-06-24\` |
| \`HDFC_ENABLE_LOGGING\` | \`false\` by default; when true logs method/path/status only |
| \`HDFC_TIMEOUT_SECONDS\` | HTTP timeout, default 30 |

## Payment flow

1. Your backend creates an order in its own database and generates a unique alphanumeric order ID (maximum 20 characters).
2. Your backend calls \`POST /api/payments/session\` with amount, customer details, and the public HTTPS return URL.
3. This application sends a real authenticated \`POST /session\` request to HDFC UAT.
4. HDFC creates the order and responds with \`payment_links\` and/or \`sdk_payload\`.
5. Your frontend opens the HDFC-provided payment link (or uses the SDK payload in the appropriate frontend SDK).
6. HDFC redirects the customer to the return URL.
7. Verify the signed return parameters, then call the HDFC order-status endpoint. Only mark the order paid after the server-side HDFC status confirms the successful terminal state.

## Create a payment session

**POST** \`http://localhost:5000/api/payments/session\`

Example request:

\`\`\`json
{
  "orderId": "ORD202611230001",
  "amount": 10.00,
  "currency": "INR",
  "customerId": "CUSTOMER1001",
  "customerEmail": "customer@example.com",
  "customerPhone": "9876543210",
  "returnUrl": "https://your-public-domain.example/api/payments/return",
  "firstName": "Test",
  "lastName": "Customer",
  "description": "Sandbox payment test"
}
\`\`\`

The response is HDFC's actual response. It should contain the order status and payment link/SDK payload as defined by HDFC. Use the returned payment link; do not construct one yourself.

## Check order status

**GET** \`http://localhost:5000/api/payments/status/ORD202611230001?customerId=CUSTOMER1001\`

This makes an authenticated server-to-server request to:

\`\`\`
GET https://smartgateway.hdfcuat.bank.in/orders/{order_id}
\`\`\`

The same routing/customer ID used for the session should be supplied when checking status. The response is HDFC's actual order-status response.

## Return URL signature verification

HDFC's signed return flow uses \`signature\` and \`signature_algorithm\` query parameters. This implementation supports \`HMAC-SHA256\` using the configured response key and HDFC's documented canonicalization:

- Exclude \`signature\` and \`signature_algorithm\`.
- URL-encode parameter keys and values.
- Sort by encoded key.
- Join as \`key=value\` pairs with ampersands.
- URL-encode the joined string.
- Calculate HMAC-SHA256 using the response key.
- Base64-encode and URL-encode the hash before comparing.

The endpoint returns whether the signature was valid. **A valid redirect signature is not itself proof that an order is paid.** Always query HDFC order status before fulfilling goods/services.

Enable signed responses in the HDFC merchant dashboard if required for this flow. The return URL must be a public HTTPS endpoint, have no query parameters in the configured base URL, and must not redirect to another URL.

## Security and production readiness

- \`.env\` is ignored by Git. Do not commit API keys or response keys.
- Never expose credentials to browser/mobile clients.
- Keep UAT and production credentials and URLs separate.
- This build is intentionally UAT-only. Do not switch the base URL to production without a separate production review and explicit code/config changes.
- The API endpoints in this POC are a development integration surface. Before public deployment, add your application's authentication/authorization, order ownership checks, rate limiting, durable order persistence, structured redacted audit logs, and monitoring.
- Do not trust a client-supplied amount or mark an order paid based on a browser redirect.
- Do not log Authorization headers, API keys, response keys, or full sensitive payment payloads.

## Not yet included

This POC covers session creation, order status lookup, and signed return verification. Refunds, cancellations, capture/void flows, webhook event handling, frontend SDK integration, and durable merchant order storage should be added only against the merchant's exact enabled HDFC product/API specification.

## Documentation

- [HDFC SmartGateway developer portal](https://smartgateway.hdfc.bank.in/docs/)
- [HDFC SmartGateway API reference – Basic Authentication](https://smartgateway.hdfc.bank.in/docs/smartgateway-api-ref-basicauth/docs/overview/integration-architecture)
- [HDFC SmartGateway session API overview](https://juspay.io/in/docs/smartgateway-api-reference/docs/apis/session)
- [HDFC order status API reference](https://juspay.io/sea/docs/api-reference/docs/express-checkout/get-offer-order-status)
- [HDFC signed return URL verification](https://juspay.io/sea/docs/hypercheckout-mobile-sdk/web/resources/hmac-signature-verification-for-return-url)

## License

MIT.
