using System;
using System.Configuration;

namespace HDFCSmartGateway.Services
{
    public class HdfcSettings
    {
        public string BaseUrl { get; private set; }
        public string MerchantId { get; private set; }
        public string ApiKey { get; private set; }
        public string PaymentPageClientId { get; private set; }
        public string ResponseKey { get; private set; }
        public string ReturnUrl { get; private set; }
        public string SessionPath { get; private set; }
        public string OrderStatusPath { get; private set; }
        public string RoutingHeaderName { get; private set; }
        public bool VerifyReturnSignature { get; private set; }
        public bool EnableLogging { get; private set; }

        public static HdfcSettings Load()
        {
            return new HdfcSettings
            {
                BaseUrl = Get("HDFC_BASE_URL", "https://smartgateway.hdfcuat.bank.in"),
                MerchantId = Get("HDFC_MERCHANT_ID", "SG6695"),
                ApiKey = Get("HDFC_API_KEY", ""),
                PaymentPageClientId = Get("HDFC_PAYMENT_PAGE_CLIENT_ID", "hdfcmaster"),
                ResponseKey = Get("HDFC_RESPONSE_KEY", ""),
                ReturnUrl = Get("HDFC_RETURN_URL", ""),
                SessionPath = Get("HDFC_SESSION_PATH", "/session"),
                OrderStatusPath = Get("HDFC_ORDER_STATUS_PATH", "/orders/{orderId}"),
                RoutingHeaderName = Get("HDFC_ROUTING_HEADER_NAME", "x-routing-id"),
                VerifyReturnSignature = string.Equals(Get("HDFC_VERIFY_RETURN_SIGNATURE", "false"), "true", StringComparison.OrdinalIgnoreCase),
                EnableLogging = string.Equals(Get("HDFC_ENABLE_LOGGING", "false"), "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        public string Validate()
        {
            Uri baseUri;
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
                return "HDFC_BASE_URL must be a valid HTTPS URL.";
            if (!string.Equals(baseUri.Host, "smartgateway.hdfcuat.bank.in", StringComparison.OrdinalIgnoreCase))
                return "This POC is restricted to the HDFC UAT host.";
            if (string.IsNullOrWhiteSpace(MerchantId)) return "Set HDFC_MERCHANT_ID.";
            if (string.IsNullOrWhiteSpace(ApiKey)) return "Set HDFC_API_KEY in the server environment.";
            if (string.IsNullOrWhiteSpace(PaymentPageClientId)) return "Set HDFC_PAYMENT_PAGE_CLIENT_ID.";
            if (string.IsNullOrWhiteSpace(SessionPath) || !SessionPath.StartsWith("/")) return "HDFC_SESSION_PATH must start with /.";
            if (string.IsNullOrWhiteSpace(OrderStatusPath) || !OrderStatusPath.StartsWith("/")) return "HDFC_ORDER_STATUS_PATH must start with /.";
            if (VerifyReturnSignature && string.IsNullOrWhiteSpace(ResponseKey)) return "Set HDFC_RESPONSE_KEY or disable signed-return verification.";
            return null;
        }

        private static string Get(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
