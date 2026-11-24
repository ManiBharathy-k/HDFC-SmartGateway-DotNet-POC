
using System;

namespace HDFCSmartGatewayPOC.Configuration
{
    public class HdfcSettings
    {
        public string ApiKey { get; private set; }
        public string MerchantId { get; private set; }
        public string BaseUrl { get; private set; }
        public string PaymentPageClientId { get; private set; }
        public string ResponseKey { get; private set; }
        public bool EnableLogging { get; private set; }

        public static HdfcSettings Load()
        {
            return new HdfcSettings
            {
                ApiKey = Environment.GetEnvironmentVariable("HDFC_API_KEY"),
                MerchantId = GetValue("HDFC_MERCHANT_ID", "SG6695"),
                BaseUrl = GetValue(
                    "HDFC_BASE_URL",
                    "https://smartgateway.hdfcuat.bank.in"
                ),
                PaymentPageClientId = GetValue(
                    "HDFC_PAYMENT_PAGE_CLIENT_ID",
                    "hdfcmaster"
                ),
                ResponseKey = Environment.GetEnvironmentVariable(
                    "HDFC_RESPONSE_KEY"
                ),
                EnableLogging = GetValue("HDFC_ENABLE_LOGGING", "false")
                    .Equals("true", StringComparison.OrdinalIgnoreCase)
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException(
                    "HDFC_API_KEY environment variable is missing.");

            if (string.IsNullOrWhiteSpace(MerchantId))
                throw new InvalidOperationException("Merchant ID is missing.");

            if (string.IsNullOrWhiteSpace(BaseUrl))
                throw new InvalidOperationException("Base URL is missing.");
        }

        private static string GetValue(string name, string defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);

            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value;
        }
    }
}
