using System;
using System.Configuration;

namespace HDFCSmartGatewayPOC.Configuration
{
    public sealed class HdfcSettings
    {
        public string ApiKey { get; private set; }
        public string MerchantId { get; private set; }
        public string BaseUrl { get; private set; }
        public string PaymentPageClientId { get; private set; }
        public string ResponseKey { get; private set; }
        public string ReturnUrl { get; private set; }
        public bool EnableLogging { get; private set; }
        public int TimeoutSeconds { get; private set; }

        public static HdfcSettings Load()
        {
            return new HdfcSettings
            {
                ApiKey = Environment.GetEnvironmentVariable("HDFC_API_KEY") ?? ConfigurationManager.AppSettings["HDFC_API_KEY"],
                MerchantId = GetValue("HDFC_MERCHANT_ID", "SG6695"),
                BaseUrl = GetValue("HDFC_BASE_URL", "https://smartgateway.hdfcuat.bank.in"),
                PaymentPageClientId = GetValue("HDFC_PAYMENT_PAGE_CLIENT_ID", "hdfcmaster"),
                ResponseKey = Environment.GetEnvironmentVariable("HDFC_RESPONSE_KEY") ?? ConfigurationManager.AppSettings["HDFC_RESPONSE_KEY"],
                ReturnUrl = GetValue("HDFC_RETURN_URL", ""),
                EnableLogging = GetValue("HDFC_ENABLE_LOGGING", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
                TimeoutSeconds = ParseTimeout(GetValue("HDFC_TIMEOUT_SECONDS", "30"))
            };
        }

        public void Validate()
        {
            Uri uri;
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !String.Equals(uri.Host, "smartgateway.hdfcuat.bank.in", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This POC is sandbox-only. HDFC_BASE_URL must be https://smartgateway.hdfcuat.bank.in.");
            if (String.IsNullOrWhiteSpace(ApiKey)) throw new InvalidOperationException("Set HDFC_API_KEY in a user/machine environment variable.");
            if (String.IsNullOrWhiteSpace(MerchantId)) throw new InvalidOperationException("HDFC_MERCHANT_ID is required.");
            if (String.IsNullOrWhiteSpace(PaymentPageClientId)) throw new InvalidOperationException("HDFC_PAYMENT_PAGE_CLIENT_ID is required.");
        }

        private static string GetValue(string key, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(key);
            if (String.IsNullOrWhiteSpace(value)) value = ConfigurationManager.AppSettings[key];
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        private static int ParseTimeout(string value)
        {
            int seconds; if (!Int32.TryParse(value, out seconds)) seconds = 30;
            return Math.Max(5, Math.Min(120, seconds));
        }
    }
}
