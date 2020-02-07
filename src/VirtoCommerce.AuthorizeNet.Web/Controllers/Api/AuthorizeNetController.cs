using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AuthorizeNet.Web.Managers;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.PaymentModule.Core.Model.Search;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.AuthorizeNet.Web.Controllers.Api
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/payments/an")]
    public class AuthorizeNetController : Controller
    {
        private readonly ICustomerOrderService _customerOrderService;
        private readonly IStoreService _storeService;
        private readonly IPaymentMethodsSearchService _paymentMethodsSearchService;

        public AuthorizeNetController(ICustomerOrderService customerOrderService, IStoreService storeService, IPaymentMethodsSearchService paymentMethodsSearchService)
        {
            _customerOrderService = customerOrderService;
            _storeService = storeService;
            _paymentMethodsSearchService = paymentMethodsSearchService;
        }

        [HttpPost]
        [Route("registerpayment/{orderId}")]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [AllowAnonymous]
        public async Task<ActionResult> RegisterPayment(string orderId)
        {
            var parameters = new NameValueCollection();

            foreach (var key in HttpContext.Request.Query.Keys)
            {
                parameters.Add(key, HttpContext.Request.Query[key]);
            }

            foreach (var key in HttpContext.Request.Form.Keys)
            {
                parameters.Add(key, HttpContext.Request.Form[key]);
            }

            var order = (await _customerOrderService.GetByIdsAsync(new[] { orderId })).FirstOrDefault();
            if (order == null)
            {
                throw new ArgumentException("Order for specified orderId not found.", "orderId");
            }

            var store = await _storeService.GetByIdAsync(order.StoreId);

            var paymentMethodsSearchCriteria = AbstractTypeFactory<PaymentMethodsSearchCriteria>.TryCreateInstance();
            paymentMethodsSearchCriteria.StoreId = store.Id;
            paymentMethodsSearchCriteria.Codes = new[] { nameof(AuthorizeNetMethod) };
            paymentMethodsSearchCriteria.IsActive = true;

            var authorizePaymentMethods = await _paymentMethodsSearchService.SearchPaymentMethodsAsync(paymentMethodsSearchCriteria);
            var paymentMethod = authorizePaymentMethods.Results.FirstOrDefault(x => x.Code.EqualsInvariant(nameof(AuthorizeNetMethod)));

            if (paymentMethod != null)
            {
                var validateResult = paymentMethod.ValidatePostProcessRequest(parameters);
                var paymentOuterId = validateResult.OuterId;

                var payment = order.InPayments.FirstOrDefault(x => x.GatewayCode.EqualsInvariant(nameof(AuthorizeNetMethod)) && x.Sum == Convert.ToDecimal(parameters["x_amount"], CultureInfo.InvariantCulture));
                if (payment == null)
                {
                    throw new ArgumentException("payment");
                }

                var context = new PostProcessPaymentRequest
                {
                    Order = order,
                    Payment = payment,
                    Store = store,
                    OuterId = paymentOuterId,
                    Parameters = parameters
                };

                var retVal = paymentMethod.PostProcessPayment(context);

                if (retVal != null && retVal.IsSuccess)
                {
                    await _customerOrderService.SaveChangesAsync(new CustomerOrder[] { order });

                    var returnHtml = string.Format("<html><head><script type='text/javascript' charset='utf-8'>window.location='{0}';</script><noscript><meta http-equiv='refresh' content='1;url={0}'></noscript></head><body></body></html>", retVal.ReturnUrl);

                    return Ok(returnHtml);
                }
            }

            return NoContent();
        }
    }
}
