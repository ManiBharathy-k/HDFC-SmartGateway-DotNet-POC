
namespace HDFCSmartGatewayPOC.Models
{
    public class PaymentResponse
    {
        public string OrderId { get; set; }

        public string Status { get; set; }

        public string PaymentUrl { get; set; }

        public string SessionId { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public bool IsSuccessful { get; set; }
    }
}
