using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.AuthorizeNet.Web
{
	public static class ModuleConstants
	{
		public static class Settings
		{
			public static class AuthorizeNet
			{
				public static readonly SettingDescriptor ApiLogin = new SettingDescriptor
				{
					Name = "VirtoCommerce.Payment.AuthorizeNet.ApiLogin",
					GroupName = "Payment|Authorize.Net",
					ValueType = SettingValueType.ShortText,
					IsDictionary = false
				};

				public static IEnumerable<SettingDescriptor> Settings
				{
					get
					{
						return new List<SettingDescriptor>
						{
							ApiLogin,
						};
					}
				}
			}

			public static IEnumerable<SettingDescriptor> AllSettings
			{
				get
				{
					return AuthorizeNet.Settings;
				}
			}
		}
	}
}
