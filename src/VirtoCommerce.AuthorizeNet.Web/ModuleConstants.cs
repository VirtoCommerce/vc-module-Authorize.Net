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


                public static readonly SettingDescriptor Mode = new SettingDescriptor
                {
                    Name = "VirtoCommerce.Payment.AuthorizeNet.Mode",
                    GroupName = "Payment|Authorize.Net",
                    ValueType = SettingValueType.ShortText,
                    AllowedValues = new[] { "test", "real" },
                    DefaultValue = "test",
                };

                public static readonly SettingDescriptor ConfirmationUrl = new SettingDescriptor
                {
                    Name = "VirtoCommerce.Payment.AuthorizeNet.ConfirmationUrl",
                    GroupName = "Payment|Authorize.Net",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "{VC manager URL}/api/payments/an/registerpayment"
                };

                public static readonly SettingDescriptor ThankYouPageUrl = new SettingDescriptor
                {
                    Name = "VirtoCommerce.Payment.AuthorizeNet.ThankYouPageUrl",
                    GroupName = "Payment|Authorize.Net",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "{storefront URL}/cart/thanks",
                };

                public static readonly SettingDescriptor PaymentActionType = new SettingDescriptor
                {
                    Name = "VirtoCommerce.Payment.AuthorizeNet.PaymentActionType",
                    GroupName = "Payment|Authorize.Net",
                    ValueType = SettingValueType.ShortText,
                    AllowedValues = new[] { "Authorization/Capture", "Sale" },
                    DefaultValue = "Authorization/Capture",
                };

                public static IEnumerable<SettingDescriptor> Settings
                {
                    get
                    {
                        return new List<SettingDescriptor>
                        {
                            Mode,
                            ConfirmationUrl,
                            ThankYouPageUrl,
                            PaymentActionType,
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
