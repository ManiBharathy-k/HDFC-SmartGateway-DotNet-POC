using System;
using System.ComponentModel.DataAnnotations;

namespace HDFCSmartGateway.Models
{
    public class CreatePaymentViewModel
    {
        [Required, StringLength(20, MinimumLength = 1)]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Use only letters and numbers for the order ID.")]
        [Display(Name = "Order ID")]
        public string OrderId { get; set; }

        [Required, Range(typeof(decimal), "0.01", "99999999")]
        [Display(Name = "Amount (INR)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        [Display(Name = "Customer ID")]
        public string CustomerId { get; set; }

        [EmailAddress, StringLength(200)]
        [Display(Name = "Customer email")]
        public string CustomerEmail { get; set; }

        [Phone, StringLength(20)]
        [Display(Name = "Customer phone")]
        public string CustomerPhone { get; set; }

        [StringLength(80)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [StringLength(80)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [StringLength(250)]
        public string Description { get; set; }

        [Url]
        [Display(Name = "Return URL (public HTTPS)")]
        public string ReturnUrl { get; set; }
    }

    public class PaymentResultViewModel
    {
        public string OrderId { get; set; }
        public string GatewayStatusCode { get; set; }
        public string GatewayResponse { get; set; }
        public string PaymentUrl { get; set; }
        public string Message { get; set; }
    }

    public class PaymentStatusViewModel
    {
        [Required]
        public string OrderId { get; set; }
        [Required]
        public string CustomerId { get; set; }
    }
}
