using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.AuthorizeNet.Web.Services;

namespace VirtoCommerce.AuthorizeNet.Web.Controllers.Api
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/payments/an")]
    public class AuthorizeNetController : Controller
    {
        private readonly IAuthorizeNetRegisterPaymentService _authorizeNetRegisterPaymentService;

        public AuthorizeNetController(IAuthorizeNetRegisterPaymentService authorizeNetRegisterPaymentService)
        {
            _authorizeNetRegisterPaymentService = authorizeNetRegisterPaymentService;
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

            var result = await _authorizeNetRegisterPaymentService.RegisterPaymentAsync(orderId, parameters);

            return !string.IsNullOrEmpty(result) ? Ok(result) : (ActionResult)NoContent();
        }
    }
}
