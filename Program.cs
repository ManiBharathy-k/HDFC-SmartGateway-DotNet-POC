using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("HdfcGateway", client =>
{
    var timeout = int.TryParse(
        Environment.GetEnvironmentVariable("HDFC_TIMEOUT_SECONDS"), out var seconds)
        ? Math.Clamp(seconds, 5, 120)
        : 30;
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    application = "HDFC SmartGateway .NET Integration",
    mode = "SANDBOX",
    status = "Running",
    endpoints = new[]
    {
        "POST /api/payments/create",
        "GET /api/payments/status/{orderId}",
        "GET /health"
    }
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Sends the supplied HDFC request JSON to the configured sandbox create-order endpoint.
// The JSON contract is intentionally supplied by the caller, following the merchant's
// exact HDFC Basic Auth API reference; no undocumented request fields are invented.
app.MapPost("/api/payments/create", async (
    JsonElement payload,
    IHttpClientFactory clients,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        return Results.BadRequest(new { error = "A JSON request body is required." });

    var config = GatewayConfig.Load();
    var configError = config.Validate(requireCreatePath: true);
    if (configError is not null)
        return Results.Problem(title: "HDFC sandbox configuration is incomplete",
            detail: configError, statusCode: StatusCodes.Status503ServiceUnavailable);

    return await ForwardAsync(
        clients.CreateClient("HdfcGateway"),
        config,
        HttpMethod.Post,
        config.CreateOrderPath!,
        payload.GetRawText(),
        context,
        cancellationToken);
});

// The status path is configured as a template, for example:
// /the/documented/path/{orderId}. The exact path must come from HDFC's API reference.
app.MapGet("/api/payments/status/{orderId}", async (
    string orderId,
    IHttpClientFactory clients,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var config = GatewayConfig.Load();
    var configError = config.Validate(requireCreatePath: false);
    if (configError is not null)
        return Results.Problem(title: "HDFC sandbox configuration is incomplete",
            detail: configError, statusCode: StatusCodes.Status503ServiceUnavailable);

    if (string.IsNullOrWhiteSpace(config.OrderStatusPathTemplate))
        return Results.Problem(title: "HDFC status endpoint is not configured",
            detail: "Set HDFC_ORDER_STATUS_PATH_TEMPLATE to the exact status path from HDFC's API reference.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var path = config.OrderStatusPathTemplate.Replace(
        "{orderId}", Uri.EscapeDataString(orderId), StringComparison.Ordinal);

    return await ForwardAsync(
        clients.CreateClient("HdfcGateway"),
        config,
        HttpMethod.Get,
        path,
        body: null,
        context,
        cancellationToken);
});

app.Run();

static async Task<IResult> ForwardAsync(
    HttpClient client,
    GatewayConfig config,
    HttpMethod method,
    string path,
    string? body,
    HttpContext context,
    CancellationToken cancellationToken)
{
    if (!path.StartsWith("/", StringComparison.Ordinal) ||
        path.StartsWith("//", StringComparison.Ordinal) ||
        path.Contains("://", StringComparison.Ordinal))
    {
        return Results.Problem(title: "Invalid gateway path",
            detail: "Gateway paths must be relative API paths beginning with one slash.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var target = new Uri(config.BaseUrl!.TrimEnd('/') + "/" + path.TrimStart('/'), UriKind.Absolute);
    using var request = new HttpRequestMessage(method, target);

    var credentials = Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    if (!string.IsNullOrWhiteSpace(config.MerchantHeaderName) &&
        !string.IsNullOrWhiteSpace(config.MerchantId))
    {
        request.Headers.TryAddWithoutValidation(
            config.MerchantHeaderName, config.MerchantId);
    }

    if (body is not null)
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

    try
    {
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString()
            ?? "application/json";

        context.Response.Headers.CacheControl = "no-store";
        return Results.Content(
            responseBody,
            contentType,
            statusCode: (int)response.StatusCode);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(title: "HDFC sandbox request timed out",
            statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (HttpRequestException ex)
    {
        // Do not log or return credentials. The exception message is limited to transport details.
        return Results.Problem(title: "Unable to reach HDFC sandbox",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
}

sealed record GatewayConfig(
    string? BaseUrl,
    string? Username,
    string? Password,
    string? MerchantId,
    string? MerchantHeaderName,
    string? CreateOrderPath,
    string? OrderStatusPathTemplate)
{
    public static GatewayConfig Load() => new(
        Environment.GetEnvironmentVariable("HDFC_BASE_URL"),
        Environment.GetEnvironmentVariable("HDFC_BASIC_AUTH_USERNAME"),
        Environment.GetEnvironmentVariable("HDFC_BASIC_AUTH_PASSWORD"),
        Environment.GetEnvironmentVariable("HDFC_MERCHANT_ID"),
        Environment.GetEnvironmentVariable("HDFC_MERCHANT_HEADER_NAME"),
        Environment.GetEnvironmentVariable("HDFC_CREATE_ORDER_PATH"),
        Environment.GetEnvironmentVariable("HDFC_ORDER_STATUS_PATH_TEMPLATE"));

    public string? Validate(bool requireCreatePath)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps &&
             !baseUri.IsLoopback))
            return "Set HDFC_BASE_URL to the official HTTPS sandbox base URL.";

        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
            return "Set HDFC_BASIC_AUTH_USERNAME and HDFC_BASIC_AUTH_PASSWORD.";

        if (requireCreatePath && string.IsNullOrWhiteSpace(CreateOrderPath))
            return "Set HDFC_CREATE_ORDER_PATH to the exact create-order path from HDFC's API reference.";

        return null;
    }
}
