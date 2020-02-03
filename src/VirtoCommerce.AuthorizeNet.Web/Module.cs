using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
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
			settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.AuthorizeNet.Settings, nameof(AuthorizeNetMethod));

			var settingsManager = appBuilder.ApplicationServices.GetRequiredService<ISettingsManager>();

			Func<AuthorizeNetMethod> authorizeNetPaymentMethodFactory = () =>
			{
				var result = new AuthorizeNetMethod
				{
					LogoUrl = "https://raw.githubusercontent.com/VirtoCommerce/vc-module-Authorize.Net/master/Authorize.Net/Content/Authorizenet_logo.png",
					IsActive = false,
				};
				var settingNames = ModuleConstants.Settings.AuthorizeNet.Settings.Select(x => x.Name);
				var settings = settingsManager.GetObjectSettingsAsync(settingNames, result.TypeName, result.Id).GetAwaiter().GetResult().ToList();

				result.Settings = settings;

				return result;
			};

			var paymentMethodsRegistrar = appBuilder.ApplicationServices.GetRequiredService<IPaymentMethodsRegistrar>();
			paymentMethodsRegistrar.RegisterPaymentMethod(authorizeNetPaymentMethodFactory);
		}

		public void Uninstall()
		{
			// No need in actions
		}
		#endregion
	}
}
