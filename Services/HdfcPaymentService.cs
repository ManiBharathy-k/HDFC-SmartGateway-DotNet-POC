using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HDFCSmartGateway.Services
{
    public class HdfcPaymentService
    {
        private readonly HdfcSettings settings;

        public HdfcPaymentService(HdfcSettings settings)
        {
            this.settings = settings;
        }

        public async Task<string> CreateSessionAsync(Models.CreatePaymentViewModel input)
        {
            var payload = new Dictionary<string, object>
            {
                { "order_id", input.OrderId },
                { "amount", input.Amount.ToString("0.00", CultureInfo.InvariantCulture) },
                { "currency", "INR" },
                { "customer_id", input.CustomerId ?? input.OrderId },
                { "payment_page_client_id", settings.PaymentPageClientId },
                { "action", "paymentPage" },
                { "return_url", string.IsNullOrWhiteSpace(input.ReturnUrl) ? settings.ReturnUrl : input.ReturnUrl }
            };
            AddIfPresent(payload, "customer_email", input.CustomerEmail);
            AddIfPresent(payload, "customer_phone", input.CustomerPhone);
            AddIfPresent(payload, "first_name", input.FirstName);
            AddIfPresent(payload, "last_name", input.LastName);
            AddIfPresent(payload, "description", input.Description);

            return await SendAsync(HttpMethod.Post, settings.SessionPath, payload,
                input.CustomerId ?? input.OrderId).ConfigureAwait(false);
        }

        public async Task<string> GetOrderStatusAsync(string orderId, string customerId)
        {
            var path = settings.OrderStatusPath.Replace("{orderId}", Uri.EscapeDataString(orderId));
            return await SendAsync(HttpMethod.Get, path, null, customerId).ConfigureAwait(false);
        }

        private async Task<string> SendAsync(HttpMethod method, string path, object payload, string routingId)
        {
            var timeout = 30;
            int parsed;
            if (int.TryParse(Environment.GetEnvironmentVariable("HDFC_TIMEOUT_SECONDS"), out parsed))
                timeout = Math.Max(5, Math.Min(parsed, 120));

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(timeout);
                var baseUrl = settings.BaseUrl.TrimEnd('/');
                var request = new HttpRequestMessage(method, baseUrl + path);
                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.ApiKey + ":"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                request.Headers.Add("x-merchantid", settings.MerchantId);
                if (!string.IsNullOrWhiteSpace(routingId) && !string.IsNullOrWhiteSpace(settings.RoutingHeaderName))
                    request.Headers.Add(settings.RoutingHeaderName, routingId);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (payload != null)
                    request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using (request)
                using (var response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new HdfcGatewayException((int)response.StatusCode,
                            "HDFC returned HTTP " + (int)response.StatusCode + ". Response: " + Limit(content, 1500));
                    return content;
                }
            }
        }

        public bool VerifyReturn(IDictionary<string, string> parameters, string signature, string algorithm)
        {
            if (!settings.VerifyReturnSignature || string.IsNullOrWhiteSpace(settings.ResponseKey))
                return false;
            if (!string.Equals(algorithm, "HMAC-SHA256", StringComparison.OrdinalIgnoreCase))
                return false;

            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in parameters)
            {
                if (item.Key == "signature" || item.Key == "signature_algorithm") continue;
                values[item.Key] = item.Value ?? "";
            }

            var pairs = new List<string>();
            foreach (var item in values)
                pairs.Add(Uri.EscapeDataString(item.Key) + "=" + Uri.EscapeDataString(item.Value));
            var canonical = Uri.EscapeDataString(string.Join("&", pairs));
            byte[] hash;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(settings.ResponseKey)))
                hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var expected = Convert.ToBase64String(hash);
            return FixedTimeEquals(expected, signature ?? "");
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var left = Encoding.UTF8.GetBytes(a);
            var right = Encoding.UTF8.GetBytes(b);
            if (left.Length != right.Length) return false;
            var diff = 0;
            for (var i = 0; i < left.Length; i++) diff |= left[i] ^ right[i];
            return diff == 0;
        }

        private static void AddIfPresent(IDictionary<string, object> target, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target[key] = value;
        }

        private static string Limit(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "...";
        }
    }

    public class HdfcGatewayException : Exception
    {
        public int StatusCode { get; private set; }
        public HdfcGatewayException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
