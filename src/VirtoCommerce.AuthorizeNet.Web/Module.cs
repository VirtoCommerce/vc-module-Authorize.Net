using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.AuthorizeNet.Web.Managers;
using VirtoCommerce.AuthorizeNet.Web.Services;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.AuthorizeNet.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        public IConfiguration Configuration { get; set; }

        #region IModule Members

        public void Initialize(IServiceCollection serviceCollection)
        {
            var snapshot = serviceCollection.BuildServiceProvider();

            serviceCollection.AddOptions<AuthorizeNetSecureOptions>().Bind(Configuration.GetSection("Payments:AuthorizeNet")).ValidateDataAnnotations();
            serviceCollection.AddTransient<IAuthorizeNetRegisterPaymentService, AuthorizeNetRegisterPaymentService>();
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            var authorizeNetOptions = appBuilder.ApplicationServices.GetRequiredService<IOptions<AuthorizeNetSecureOptions>>();
            var paymentMethodsRegistrar = appBuilder.ApplicationServices.GetRequiredService<IPaymentMethodsRegistrar>();
            paymentMethodsRegistrar.RegisterPaymentMethod(() => new AuthorizeNetMethod(authorizeNetOptions, appBuilder.ApplicationServices.GetService<IHttpClientFactory>()));

            settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.AuthorizeNet.Settings, nameof(AuthorizeNetMethod));
        }

        public void Uninstall()
        {
            // No need in actions
        }
        #endregion
    }
}
