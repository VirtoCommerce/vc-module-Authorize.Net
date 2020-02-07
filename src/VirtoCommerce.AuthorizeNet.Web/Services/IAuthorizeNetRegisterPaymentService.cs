using System.Collections.Specialized;
using System.Threading.Tasks;

namespace VirtoCommerce.AuthorizeNet.Web.Services
{
    public interface IAuthorizeNetRegisterPaymentService
    {
        Task<string> RegisterPaymentAsync(string orderId, NameValueCollection paymentParameters);
    }
}
