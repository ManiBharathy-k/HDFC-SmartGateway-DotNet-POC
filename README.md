# HDFC SmartGateway – .NET POC

A single-file ASP.NET Core proof of concept for demonstrating a payment lifecycle before HDFC SmartGateway sandbox credentials are available.

> **Important:** This repository currently runs in **MOCK mode only**. It does not connect to HDFC, create HDFC orders, redirect to an HDFC-hosted checkout, verify HDFC transactions, or receive authenticated HDFC callbacks. Do not present this mock as a completed HDFC integration.

## What this POC demonstrates

- Create a local payment order with POST.
- Open a local mock checkout page.
- Simulate success or failure.
- Retrieve the local order status with GET.
- Show a return URL pattern.
- Keep the callback endpoint disabled until HDFC's actual callback authentication and payload contract are implemented.

## Requirements

- .NET 8 SDK or later
- Postman or curl (optional)

No third-party NuGet packages are required.

## Run locally

```bash
dotnet new web -n HDFCSmartGatewayPOC
cd HDFCSmartGatewayPOC
```

Replace the generated `Program.cs` with the `Program.cs` in this repository, then run:

```bash
dotnet run --urls http://localhost:5000
```

Open `http://localhost:5000`.

## API endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/` | Health and endpoint overview |
| POST | `/api/payment/create` | Create a local mock order |
| GET | `/mock-checkout/{orderId}` | Open mock checkout |
| POST | `/api/payment/mock-complete/{orderId}` | Simulate success/failure |
| GET | `/api/payment/status/{orderId}` | Read local mock status |
| GET | `/api/payment/return?orderId={orderId}` | Demonstrate browser return |
| POST | `/api/payment/callback` | Disabled placeholder (501) |

## Test with Postman

### 1. Create an order

**POST** `http://localhost:5000/api/payment/create`

```json
{
  "amount": 500,
  "currency": "INR",
  "email": "customer@example.com",
  "customerName": "Test Customer"
}
```

The response includes an `orderId` and a local `checkoutUrl`.

### 2. Open the mock checkout

Open the returned `checkoutUrl` in a browser. Choose **Simulate Success** or **Simulate Failure**.

### 3. Check status

**GET** `http://localhost:5000/api/payment/status/{orderId}`

Replace `{orderId}` with the ID returned by the create endpoint.

### 4. Simulate completion directly

**POST** `http://localhost:5000/api/payment/mock-complete/{orderId}`

```json
{
  "status": "SUCCESS"
}
```

Use `FAILED` to test a failed payment. A processed order cannot be processed again.

## Configuration and URLs

The POC uses `http://localhost:5000` as its local public base URL. For local testing on another port, set `Payment:PublicBaseUrl` to match the URL used to run the app.

For a hosted deployment, use the application's public HTTPS origin. The final HDFC return URL and webhook/callback URL requirements must be confirmed from HDFC's merchant-specific onboarding and API documentation. A browser return URL and a server-to-server webhook are different mechanisms.

## Moving from MOCK to HDFC sandbox

When the customer receives HDFC sandbox access, implement the actual HDFC adapter using the exact current SmartGateway Basic Authentication API reference and merchant-specific details supplied by HDFC.

Obtain and verify:

- Sandbox API base URL and endpoint paths
- Merchant identifier and API username/password
- Exact create-order request and response schemas
- Required headers and Basic Authentication format
- Checkout/redirect flow and allowed return URL format
- Transaction status API contract
- Callback/webhook payload and signature/authentication verification
- Test cards/payment methods and UAT test cases

Do not assume endpoint paths, field names, authentication behavior, or callback signatures from examples for other HDFC products.

### Credential handling

- Never commit merchant credentials, API passwords, signing keys, or certificates.
- Use .NET User Secrets for local development or a managed secret store in hosted environments.
- Keep sandbox and production credentials separate.
- Do not put secrets in `appsettings.json`, source code, screenshots, Postman exports, or Git history.

Example local configuration keys (placeholders only):

```text
Payment__Mode=Mock
Payment__PublicBaseUrl=http://localhost:5000
HdfcGateway__BaseUrl=
HdfcGateway__MerchantId=
HdfcGateway__Username=
HdfcGateway__Password=
```

These are proposed application configuration names, not HDFC-prescribed parameter names.

## Important limitations

This is demonstration code, not production payment software:

- Orders are stored in memory and disappear when the process restarts.
- No real payment is initiated.
- Mock completion endpoints must never be deployed publicly.
- The callback endpoint intentionally returns `501 Not Implemented`.
- No HDFC authentication, signature verification, idempotency persistence, reconciliation, or production-grade logging is implemented.
- A browser redirect must never be treated as proof of successful payment.
- Before production, persist orders in a database, verify payment status server-to-server, authenticate callbacks according to HDFC's exact specification, and implement idempotency and reconciliation.

## Official documentation

- [HDFC SmartGateway – Integration Architecture](https://smartgateway.hdfc.bank.in/docs/smartgateway-api-ref-basicauth/docs/overview/integration-architecture)

Refer to the current API reference and HDFC's merchant-specific onboarding instructions before implementing live sandbox calls.

## License

MIT. See [LICENSE](LICENSE).
