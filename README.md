# HDFC SmartGateway – ASP.NET MVC 5 (.NET Framework 4.8)

Migrated from .NET 8 Minimal API to the classic Visual Studio ASP.NET Web Application (.NET Framework) MVC template.

## Visual Studio
- Template: ASP.NET Web Application (.NET Framework)
- Framework: .NET Framework 4.8
- Template: MVC
- Authentication: No Authentication
- NuGet: Microsoft.AspNet.Mvc 5.2.9 and Newtonsoft.Json 13.0.3

## Configure UAT
Set as Windows/user or IIS application-pool environment variables. Classic ASP.NET does not automatically load .env files:
- HDFC_BASE_URL=https://smartgateway.hdfcuat.bank.in
- HDFC_MERCHANT_ID=SG6695
- HDFC_API_KEY=actual merchant API key (never commit)
- HDFC_PAYMENT_PAGE_CLIENT_ID=hdfcmaster
- HDFC_RESPONSE_KEY=full response key only if signed returns are enabled
- HDFC_RETURN_URL=https://your-public-host/Payment/Return
- HDFC_SESSION_PATH=/session
- HDFC_ORDER_STATUS_PATH=/orders/{orderId}
- HDFC_ROUTING_HEADER_NAME=x-routing-id

## Included
MVC payment form, server-side session request, HDFC payment link response display, return landing page, and server-side order-status lookup.

## Critical integration note
The /session, /orders/{orderId}, Basic Auth construction, x-merchantid, x-routing-id and payload are provisional settings carried forward from the previous POC. They have NOT been confirmed against the exact Basic Authentication Integration Kit enabled for merchant SG6695. Confirm endpoint paths, required headers, payload schema, response schema and status rules with HDFC before testing a payment. Do not mix JWT/encrypted API requirements into a Basic Auth integration.

Response Key is not needed for Basic Auth API authentication. Signed return verification is not implemented in this MVC migration yet. A browser return is never proof of payment; always query HDFC server-to-server before fulfilling an order.

## Not production-ready
No persistent order database, auth/order ownership, webhook, idempotency, refund/cancel, or full payment state machine. Do not expose publicly without these controls.

Docs: https://smartgateway.hdfc.bank.in/docs/