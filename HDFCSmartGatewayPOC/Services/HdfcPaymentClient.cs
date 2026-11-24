
using System;
using HDFCSmartGatewayPOC.Configuration;
using HDFCSmartGatewayPOC.Models;

namespace HDFCSmartGatewayPOC.Services
{
	public class HdfcPaymentClient
	{
		private readonly HdfcSettings _settings;

		public HdfcPaymentClient(HdfcSettings settings)
		{
			_settings = settings ??
				throw new ArgumentNullException(nameof(settings));

			_settings.Validate();
		}

		// HDFC payment session creation will be implemented here.

		// HDFC payment status retrieval will be implemented here.
	}
}
