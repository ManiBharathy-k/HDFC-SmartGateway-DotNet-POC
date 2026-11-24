using HDFCSmartGateway.Models;
using HDFCSmartGateway.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace HDFCSmartGateway.Controllers
{
    public class PaymentController : Controller
    {
        private readonly HdfcSettings settings = HdfcSettings.Load();
        private HdfcPaymentService Service { get { return new HdfcPaymentService(settings); } }

        [HttpGet]
        public ActionResult Create()
        {
            return View(new CreatePaymentViewModel { Amount = 10m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CreatePaymentViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var configError = settings.Validate();
            if (configError != null)
            {
                ModelState.AddModelError("", configError);
                return View(model);
            }
            if (string.IsNullOrWhiteSpace(model.CustomerEmail) && string.IsNullOrWhiteSpace(model.CustomerPhone))
            {
                ModelState.AddModelError("", "Enter a customer email or phone number.");
                return View(model);
            }
            var callback = string.IsNullOrWhiteSpace(model.ReturnUrl) ? settings.ReturnUrl : model.ReturnUrl;
            Uri callbackUri;
            if (!Uri.TryCreate(callback, UriKind.Absolute, out callbackUri) ||
                callbackUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(callbackUri.Query))
            {
                ModelState.AddModelError("ReturnUrl", "Return URL must be a public HTTPS URL without query parameters.");
                return View(model);
            }

            try
            {
                var json = await Service.CreateSessionAsync(model);
                var parsed = JObject.Parse(json);
                var paymentUrl = (string)(parsed.SelectToken("payment_links.web") ??
                                          parsed.SelectToken("payment_links.payment_page") ??
                                          parsed.SelectToken("payment_url"));
                return View("Result", new PaymentResultViewModel
                {
                    OrderId = model.OrderId,
                    GatewayResponse = json,
                    PaymentUrl = paymentUrl,
                    Message = "HDFC UAT session response received. Open only the payment URL returned by HDFC."
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public ActionResult Return()
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in Request.QueryString.AllKeys)
                if (key != null) parameters[key] = Request.QueryString[key];

            var signature = Request.QueryString["signature"];
            var algorithm = Request.QueryString["signature_algorithm"];
            var verified = settings.VerifyReturnSignature &&
                           Service.VerifyReturn(parameters, signature, algorithm);
            ViewBag.SignatureVerified = verified;
            ViewBag.VerificationEnabled = settings.VerifyReturnSignature;
            ViewBag.OrderId = Request.QueryString["order_id"];
            ViewBag.ReturnedStatus = Request.QueryString["status"];
            return View();
        }

        [HttpGet]
        public ActionResult Status()
        {
            return View(new PaymentStatusViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Status(PaymentStatusViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var error = settings.Validate();
            if (error != null)
            {
                ModelState.AddModelError("", error);
                return View(model);
            }
            try
            {
                ViewBag.OrderId = model.OrderId;
                ViewBag.GatewayResponse = await Service.GetOrderStatusAsync(model.OrderId, model.CustomerId);
                return View("StatusResult");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }
    }
}
