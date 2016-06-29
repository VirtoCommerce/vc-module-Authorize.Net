using System;
using Microsoft.Practices.Unity;
using VirtoCommerce.Domain.Payment.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using Authorize.Net.Managers;

namespace Authorize.Net
{
    public class Module : ModuleBase
    {
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        #region IModule Members

        public override void PostInitialize()
        {
            var settings = _container.Resolve<ISettingsManager>().GetModuleSettings("Authorize.Net");

            Func<AuthorizeNetMethod> authorizeNetPaymentMethodFactory = () => new AuthorizeNetMethod
            {
                Name = "Authorize.Net payment gateway",
                Description = "Authorize.Net payment gateway integration",
                LogoUrl = "https://raw.githubusercontent.com/VirtoCommerce/vc-module-Authorize.Net/master/Authorize.Net/Content/Authorizenet_logo.png",
                Settings = settings,
                IsActive = false
            };

            _container.Resolve<IPaymentMethodsService>().RegisterPaymentMethod(authorizeNetPaymentMethodFactory);
        }

        #endregion
    }
}
