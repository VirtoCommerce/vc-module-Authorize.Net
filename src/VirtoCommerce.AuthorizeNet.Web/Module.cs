using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.AuthorizeNet.Web.Managers;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.AuthorizeNet.Web
{
    public class Module : IModule
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        #region IModule Members

        public void Initialize(IServiceCollection serviceCollection)
        {
            // No need in actions
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            var paymentMethodsRegistrar = appBuilder.ApplicationServices.GetRequiredService<IPaymentMethodsRegistrar>();
            paymentMethodsRegistrar.RegisterPaymentMethod<AuthorizeNetMethod>();
            settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.AuthorizeNet.Settings, nameof(AuthorizeNetMethod));
        }

        public void Uninstall()
        {
            // No need in actions
        }
        #endregion
    }
}
