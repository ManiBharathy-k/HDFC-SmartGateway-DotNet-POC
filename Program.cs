using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("Hdfc", client =>
{
    var seconds = int.TryParse(Environment.GetEnvironmentVariable("HDFC_TIMEOUT_SECONDS"), out var parsed)
        ? Math.Clamp(parsed, 5, 120)
        : 30;
    client.Timeout = TimeSpan.FromSeconds(seconds);
});

var app = builder.Build();
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/", () => Results.Ok(new
{
    application = "HDFC SmartGateway .NET",
    environment = "SANDBOX",
    status = "running",
    endpoints = new[]
    {
        "POST /api/payments/session",
        "GET /api/payments/status/{orderId}?customerId={customerId}",
        "GET /api/payments/return",
        "GET /health"
    }
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Creates a real HDFC SmartGateway sandbox session.
// The API key is sent as HTTP Basic Auth username with an empty password.
app.MapPost("/api/payments/session", async (
    CreatePaymentRequest input,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    var config = HdfcConfig.Load();
    var error = config.Validate(requireResponseKey: false);
    if (error is not null)
        return Results.Problem(title: "HDFC sandbox configuration is incomplete",
            detail: error, statusCode: StatusCodes.Status503ServiceUnavailable);

    var effectiveReturnUrl = string.IsNullOrWhiteSpace(input.ReturnUrl) ? config.ReturnUrl : input.ReturnUrl;
    var validation = input.Validate(effectiveReturnUrl);
    if (validation is not null)
        return Results.BadRequest(new { error = validation });

    var requestBody = new Dictionary<string, object?>
    {
        ["order_id"] = input.OrderId,
        ["amount"] = input.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        ["currency"] = string.IsNullOrWhiteSpace(input.Currency) ? "INR" : input.Currency.ToUpperInvariant(),
        ["customer_id"] = input.CustomerId ?? "",
        ["payment_page_client_id"] = config.PaymentPageClientId,
        ["action"] = "paymentPage",
        ["return_url"] = effectiveReturnUrl,
        ["customer_email"] = input.CustomerEmail,
        ["customer_phone"] = input.CustomerPhone,
        ["first_name"] = input.FirstName,
        ["last_name"] = input.LastName,
        ["description"] = input.Description
    };

    // Remove optional nulls; keep customer_id because HDFC supports an empty string for guest checkout.
    foreach (var key in requestBody.Where(x => x.Value is null).Select(x => x.Key).ToArray())
        requestBody.Remove(key);

    var body = JsonSerializer.Serialize(requestBody, jsonOptions);
    return await HdfcApi.SendAsync(
        clients.CreateClient("Hdfc"), config, HttpMethod.Post, "/session",
        body, string.IsNullOrWhiteSpace(input.CustomerId) ? input.OrderId : input.CustomerId, cancellationToken);
});

// Fetch the authoritative server-side order status from HDFC.
app.MapGet("/api/payments/status/{orderId}", async (
    string orderId,
    string customerId,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    var config = HdfcConfig.Load();
    var error = config.Validate(requireResponseKey: false);
    if (error is not null)
        return Results.Problem(title: "HDFC sandbox configuration is incomplete",
            detail: error, statusCode: StatusCodes.Status503ServiceUnavailable);

    if (string.IsNullOrWhiteSpace(orderId) || orderId.Length > 20 ||
        !orderId.All(char.IsLetterOrDigit))
        return Results.BadRequest(new { error = "orderId must be alphanumeric and no longer than 20 characters." });

    if (string.IsNullOrWhiteSpace(customerId))
        return Results.BadRequest(new { error = "customerId is required. Use the same routing/customer ID used when creating the session." });

    var path = "/orders/" + Uri.EscapeDataString(orderId);
    return await HdfcApi.SendAsync(
        clients.CreateClient("Hdfc"), config, HttpMethod.Get, path,
        null, customerId, cancellationToken);
});

// HDFC returns the customer to this URL after checkout.
// Signature validation authenticates the redirect data; it does NOT replace an order-status API call.
app.MapGet("/api/payments/return", (HttpRequest request) =>
{
    var config = HdfcConfig.Load();
    if (string.IsNullOrWhiteSpace(config.ResponseKey))
        return Results.Problem(title: "HDFC response key is not configured",
            detail: "Set HDFC_RESPONSE_KEY to verify signed return parameters.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var query = request.Query.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.ToString(),
        StringComparer.Ordinal);

    var suppliedSignature = query.TryGetValue("signature", out var sig) ? sig : null;
    var algorithm = query.TryGetValue("signature_algorithm", out var alg) ? alg : null;

    if (string.IsNullOrWhiteSpace(suppliedSignature))
        return Results.BadRequest(new { verified = false, error = "Missing signature in HDFC return parameters." });

    if (!string.Equals(algorithm, "HMAC-SHA256", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { verified = false, error = "Unsupported or missing signature_algorithm." });

    query.Remove("signature");
    query.Remove("signature_algorithm");

    var canonical = HdfcSignature.BuildCanonicalString(query);
    var expected = HdfcSignature.Compute(canonical, config.ResponseKey);
    var received = Uri.UnescapeDataString(suppliedSignature);

    var verified = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(received));

    if (!verified)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        verified = true,
        message = "HDFC return signature verified. Fetch the order status from HDFC before marking the order paid.",
        orderId = query.TryGetValue("order_id", out var id) ? id : null,
        status = query.TryGetValue("status", out var status) ? status : null,
        signatureAlgorithm = algorithm
    });
});

app.Run();

public sealed class CreatePaymentRequest
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Currency { get; set; } = "INR";
    public string? CustomerId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? ReturnUrl { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Description { get; set; }

    public string? Validate(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(OrderId) || OrderId.Length > 20 ||
            !OrderId.All(char.IsLetterOrDigit))
            return "orderId is required, must be alphanumeric, and must be no longer than 20 characters.";

        if (Amount <= 0 || decimal.Round(Amount, 2) != Amount)
            return "amount must be positive and have no more than two decimal places.";

        if (string.IsNullOrWhiteSpace(CustomerEmail) && string.IsNullOrWhiteSpace(CustomerPhone))
            return "Provide at least customerEmail or customerPhone.";

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri) ||
            returnUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(returnUri.Query) ||
            !string.IsNullOrEmpty(returnUri.UserInfo) ||
            System.Net.IPAddress.TryParse(returnUri.Host, out _))
            return "returnUrl must be a public HTTPS URL without query parameters or an IP address.";

        return null;
    }
}

public sealed record HdfcConfig(
    string BaseUrl,
    string MerchantId,
    string ApiKey,
    string PaymentPageClientId,
    string? ResponseKey,
    string? ReturnUrl,
    bool EnableLogging)
{
    public static HdfcConfig Load() => new(
        Environment.GetEnvironmentVariable("HDFC_BASE_URL") ?? "https://smartgateway.hdfcuat.bank.in",
        Environment.GetEnvironmentVariable("HDFC_MERCHANT_ID") ?? "SG6695",
        Environment.GetEnvironmentVariable("HDFC_API_KEY") ?? "",
        Environment.GetEnvironmentVariable("HDFC_PAYMENT_PAGE_CLIENT_ID") ?? "hdfcmaster",
        Environment.GetEnvironmentVariable("HDFC_RESPONSE_KEY"),
        Environment.GetEnvironmentVariable("HDFC_RETURN_URL"),
        string.Equals(Environment.GetEnvironmentVariable("HDFC_ENABLE_LOGGING"), "true", StringComparison.OrdinalIgnoreCase));

    public string? Validate(bool requireResponseKey)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
            return "HDFC_BASE_URL must be an HTTPS URL.";

        if (!string.Equals(baseUri.Host, "smartgateway.hdfcuat.bank.in", StringComparison.OrdinalIgnoreCase))
            return "This build is sandbox-only. HDFC_BASE_URL must be https://smartgateway.hdfcuat.bank.in.";

        if (string.IsNullOrWhiteSpace(MerchantId))
            return "Set HDFC_MERCHANT_ID.";

        if (string.IsNullOrWhiteSpace(ApiKey))
            return "Set HDFC_API_KEY. The supplied configuration currently has an empty API_KEY.";

        if (string.IsNullOrWhiteSpace(PaymentPageClientId))
            return "Set HDFC_PAYMENT_PAGE_CLIENT_ID (hdfcmaster for sandbox).";

        if (requireResponseKey && string.IsNullOrWhiteSpace(ResponseKey))
            return "Set HDFC_RESPONSE_KEY.";

        return null;
    }
}

public static class HdfcApi
{
    public static async Task<IResult> SendAsync(
        HttpClient client,
        HdfcConfig config,
        HttpMethod method,
        string path,
        string? body,
        string? routingId,
        CancellationToken cancellationToken)
    {
        var target = new Uri(config.BaseUrl.TrimEnd('/') + path, UriKind.Absolute);
        using var request = new HttpRequestMessage(method, target);

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ApiKey + ":"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
        request.Headers.TryAddWithoutValidation("x-merchantid", config.MerchantId);
        request.Headers.Accept.ParseAdd("application/json");

        if (!string.IsNullOrWhiteSpace(routingId))
            request.Headers.TryAddWithoutValidation("x-routing-id", routingId);

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            if (config.EnableLogging)
                Console.WriteLine("HDFC sandbox " + method + " " + path + " -> " + (int)response.StatusCode);

            return Results.Content(responseBody, contentType, statusCode: (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(title: "HDFC sandbox request timed out",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(title: "Could not connect to HDFC sandbox",
                detail: "Check the sandbox URL, DNS, TLS and network access. Credentials are not included in this error.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

public static class HdfcSignature
{
    // HDFC's documented return-URL canonicalization:
    // URL-encode key/value pairs, sort by encoded key, join with '&',
    // URL-encode the joined string, HMAC-SHA256, then URL-encode Base64 hash.
    public static string BuildCanonicalString(IDictionary<string, string> parameters)
    {
        var encoded = parameters
            .Select(pair => new
            {
                Key = Uri.EscapeDataString(pair.Key),
                Value = Uri.EscapeDataString(pair.Value)
            })
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value);

        return Uri.EscapeDataString(string.Join("&", encoded));
    }

    public static string Compute(string canonical, string responseKey)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(responseKey),
            Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(hash);
    }
}
