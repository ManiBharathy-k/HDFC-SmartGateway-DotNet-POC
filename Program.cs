using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var payments = new ConcurrentDictionary<string, Payment>();
var publicBaseUrl = builder.Configuration["Payment:PublicBaseUrl"]
    ?? "http://localhost:5000";

// Home / health
app.MapGet("/", () => Results.Ok(new
{
    Application = "HDFC SmartGateway .NET POC",
    Mode = "MOCK",
    Status = "Running",
    Endpoints = new[]
    {
        "POST /api/payment/create",
        "GET /api/payment/status/{orderId}",
        "GET /mock-checkout/{orderId}",
        "POST /api/payment/mock-complete/{orderId}",
        "GET /api/payment/return",
        "POST /api/payment/callback"
    }
}));

// Create a local mock order. This does NOT call HDFC.
app.MapPost("/api/payment/create", (CreatePaymentRequest request) =>
{
    if (request.Amount <= 0)
        return Results.BadRequest(new { message = "Amount must be greater than zero." });

    if (string.IsNullOrWhiteSpace(request.Email))
        return Results.BadRequest(new { message = "Customer email is required." });

    var orderId = "POC_" + Guid.NewGuid().ToString("N")[..16];

    var payment = new Payment
    {
        OrderId = orderId,
        Amount = request.Amount,
        Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "INR"
            : request.Currency.Trim().ToUpperInvariant(),
        Email = request.Email.Trim(),
        CustomerName = request.CustomerName?.Trim() ?? "",
        Status = "PENDING",
        CreatedAtUtc = DateTime.UtcNow
    };

    payments[orderId] = payment;

    return Results.Ok(new
    {
        success = true,
        mode = "MOCK",
        message = "Local mock order created. No request was sent to HDFC.",
        orderId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        checkoutUrl = $"{publicBaseUrl}/mock-checkout/{orderId}",
        returnUrl = $"{publicBaseUrl}/api/payment/return"
    });
});

// Mock checkout page
app.MapGet("/mock-checkout/{orderId}", (string orderId) =>
{
    if (!payments.TryGetValue(orderId, out var payment))
        return Results.NotFound("Order not found.");

    if (payment.Status != "PENDING")
        return Results.Conflict("This order has already been processed.");

    var html = $$"""
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>Payment Demo</title>
      <style>
        body{font-family:Arial,sans-serif;background:#f3f4f6;display:grid;
             place-items:center;min-height:95vh;margin:0}
        .card{background:#fff;padding:28px;border-radius:12px;width:min(360px,85vw);
              box-shadow:0 8px 30px #0001;text-align:center}
        button{width:100%;padding:13px;margin-top:12px;border:0;border-radius:7px;
               cursor:pointer;font-size:15px}
        .success{background:#15803d;color:white}
        .failure{background:#b91c1c;color:white}
        small{color:#666}
      </style>
    </head>
    <body>
      <main class="card">
        <h2>Mock Payment Checkout</h2>
        <h1>{{payment.Amount}} {{payment.Currency}}</h1>
        <p>Order: {{payment.OrderId}}</p>
        <p>{{payment.CustomerName}}</p>
        <small>Local demonstration only. This is not an HDFC checkout page.</small>
        <button class="success" onclick="complete('SUCCESS')">Simulate Success</button>
        <button class="failure" onclick="complete('FAILED')">Simulate Failure</button>
        <p id="result" role="status"></p>
      </main>
      <script>
        async function complete(status) {
          const response = await fetch('/api/payment/mock-complete/{{orderId}}', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({status})
          });
          const data = await response.json();
          document.getElementById('result').textContent =
            response.ok ? 'Mock payment: ' + data.status :
            (data.message || 'Unable to process mock payment');
          if (response.ok) {
            setTimeout(() => {
              location.href = '/api/payment/return?orderId={{orderId}}';
            }, 800);
          }
        }
      </script>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html; charset=utf-8");
});

// Mock-only payment completion. Never expose this endpoint in production.
app.MapPost("/api/payment/mock-complete/{orderId}",
    (string orderId, MockPaymentRequest request) =>
{
    if (!payments.TryGetValue(orderId, out var payment))
        return Results.NotFound(new { message = "Order not found." });

    if (payment.Status != "PENDING")
        return Results.Conflict(new { message = "Order has already been processed." });

    var requestedStatus = request.Status?.Trim().ToUpperInvariant();
    if (requestedStatus is not ("SUCCESS" or "FAILED"))
        return Results.BadRequest(new { message = "Status must be SUCCESS or FAILED." });

    payment.Status = requestedStatus;
    payment.UpdatedAtUtc = DateTime.UtcNow;

    return Results.Ok(new
    {
        success = true,
        mode = "MOCK",
        payment.OrderId,
        payment.Status
    });
});

// Local status lookup. In real integration, status must be verified server-to-server.
app.MapGet("/api/payment/status/{orderId}", (string orderId) =>
{
    if (!payments.TryGetValue(orderId, out var payment))
        return Results.NotFound(new { message = "Order not found." });

    return Results.Ok(new
    {
        payment.OrderId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        payment.CreatedAtUtc,
        payment.UpdatedAtUtc
    });
});

// Browser return URL. A redirect is NOT proof of payment.
app.MapGet("/api/payment/return", (string orderId) =>
{
    if (!payments.TryGetValue(orderId, out var payment))
        return Results.NotFound(new { message = "Order not found." });

    return Results.Ok(new
    {
        message = "Return received. Verify payment status on the server.",
        payment.OrderId,
        payment.Status,
        statusEndpoint = $"/api/payment/status/{orderId}"
    });
});

// Placeholder: do not accept or trust unsigned/unverified callbacks.
app.MapPost("/api/payment/callback", () =>
    Results.Problem(
        title: "Gateway callback is not configured",
        detail: "Implement HDFC's documented callback authentication, signature verification, and payload validation before enabling this endpoint.",
        statusCode: StatusCodes.Status501NotImplemented));

app.Run();

public sealed class CreatePaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Email { get; set; } = "";
    public string? CustomerName { get; set; }
}

public sealed class MockPaymentRequest
{
    public string? Status { get; set; }
}

public sealed class Payment
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Email { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
