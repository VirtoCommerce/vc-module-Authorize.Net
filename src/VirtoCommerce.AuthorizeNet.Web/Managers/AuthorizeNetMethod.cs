using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AuthorizeNet;
using Microsoft.Extensions.Options;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.PaymentModule.Model.Requests;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.AuthorizeNet.Web.Managers
{
    public class AuthorizeNetMethod : PaymentMethod
    {
        private readonly AuthorizeNetSecureOptions _options;

        public AuthorizeNetMethod(IOptions<AuthorizeNetSecureOptions> options) : base(nameof(AuthorizeNetMethod))
        {
            _options = options.Value;
        }

        public override PaymentMethodGroupType PaymentMethodGroupType
        {
            get
            {
                return PaymentMethodGroupType.Alternative;
            }
        }

        public override PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.PreparedForm;
            }
        }

        public string ApiLogin
        {
            get
            {
                return _options.ApiLogin;
            }
        }

        public string TxnKey
        {
            get
            {
                return _options.TxnKey;
            }
        }

        public string SHA5Hash
        {
            get
            {
                return _options.SHA2Hash;
            }
        }

        private string ConfirmationUrl
        {
            get
            {
                return Settings?.GetSettingValue(ModuleConstants.Settings.AuthorizeNet.ConfirmationUrl.Name, string.Empty);
            }
        }

        private string ThankYouPageUrl
        {
            get
            {
                return Settings?.GetSettingValue(ModuleConstants.Settings.AuthorizeNet.ThankYouPageUrl.Name, string.Empty);
            }
        }

        private string PaymentActionType
        {
            get
            {
                return Settings?.GetSettingValue(ModuleConstants.Settings.AuthorizeNet.PaymentActionType.Name, string.Empty);
            }
        }

        private string Mode
        {
            get
            {
                return Settings?.GetSettingValue(ModuleConstants.Settings.AuthorizeNet.Mode.Name, string.Empty);
            }
        }

        public override CapturePaymentRequestResult CaptureProcessPayment(CapturePaymentRequest context)
        {
            var result = AbstractTypeFactory<CapturePaymentRequestResult>.TryCreateInstance();
            var payment = context.Payment as PaymentIn ?? throw new InvalidOperationException($"\"{nameof(context.Payment)}\" should not be null and of \"{nameof(PaymentIn)}\" type.");

            using (var webClient = new WebClient())
            {
                var form = new NameValueCollection();
                form.Add("x_login", ApiLogin);
                form.Add("x_tran_key", TxnKey);

                form.Add("x_delim_data", "TRUE");
                form.Add("x_delim_char", "|");
                form.Add("x_encap_char", "");
                form.Add("x_version", ApiVersion);
                form.Add("x_method", "CC");
                form.Add("x_currency_code", payment.Currency.ToString());
                form.Add("x_type", "CAPTURE_ONLY");

                var orderTotal = Math.Round(payment.Sum, 2);
                form.Add("x_amount", orderTotal.ToString("0.00", CultureInfo.InvariantCulture));

                //x_trans_id. When x_test_request (sandbox) is set to a positive response, 
                //or when Test mode is enabled on the payment gateway, this value will be "0".
                form.Add("x_trans_id", payment.OuterId);

                var responseData = webClient.UploadValues(GetAuthorizeNetUrl(), form);
                var reply = Encoding.ASCII.GetString(responseData);

                if (!string.IsNullOrEmpty(reply))
                {
                    string[] responseFields = reply.Split('|');
                    switch (responseFields[0])
                    {
                        case "1":
                            result.NewPaymentStatus = payment.PaymentStatus = PaymentStatus.Paid;
                            payment.CapturedDate = DateTime.UtcNow;
                            result.OuterId = payment.OuterId = string.Format("{0},{1}", responseFields[6], responseFields[4]);
                            result.IsSuccess = true;
                            payment.IsApproved = true;
                            break;
                        case "2":
                            throw new InvalidOperationException(string.Format("Declined ({0}: {1})", responseFields[2], responseFields[3]));
                        case "3":
                            throw new InvalidOperationException(string.Format("Error: {0}", reply));
                    }
                }
                else
                {
                    throw new InvalidOperationException("Authorize.NET unknown error");
                }
            }

            return result;
        }

        public override PostProcessPaymentRequestResult PostProcessPayment(PostProcessPaymentRequest request)
        {
            var result = AbstractTypeFactory<PostProcessPaymentRequestResult>.TryCreateInstance();

            var payment = request.Payment as PaymentIn ?? throw new InvalidOperationException($"\"{nameof(request.Payment)}\" should not be null and of \"{nameof(PaymentIn)}\" type.");
            var order = request.Order as CustomerOrder ?? throw new InvalidOperationException($"\"{nameof(request.Order)}\" should not be null and of \"{nameof(CustomerOrder)}\" type.");
#pragma warning disable S1481 // Unused local variables should be removed
            // Need to check shop existence, though not using it
            var store = request.Store as Store ?? throw new InvalidOperationException($"\"{nameof(request.Store)}\" should not be null and of \"{nameof(Store)}\" type.");
#pragma warning restore S1481 // Unused local variables should be removed

            var transactionId = request.Parameters["x_split_tender_id"] ?? request.Parameters["x_trans_id"];
            var invoiceNumber = request.Parameters["x_invoice_num"];
            //"x_auth_code" parameter was retrieved earlier, but not used
            var totalPrice = request.Parameters["x_amount"];
            var responseCode = request.Parameters["x_response_code"];
            var responseReasonCode = request.Parameters["x_response_reason_code"];
            var responseReasonText = request.Parameters["x_response_reason_text"];
            // "x_method" parameter was retrieved earlier, but not used
            var hash = request.Parameters["x_SHA2_Hash"];
            var accountNumber = request.Parameters["x_account_number"];

            var dataString = GetDataString(request.Parameters);
            var sha2 = HMACSHA512(SHA5Hash, dataString);

            if (!string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(sha2) && string.Equals(sha2, hash, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(responseCode) && responseCode.Equals("1"))
            {
                if (PaymentActionType == "Sale")
                {
                    result.NewPaymentStatus = payment.PaymentStatus = PaymentStatus.Paid;
                    payment.Status = PaymentStatus.Paid.ToString();
                    payment.CapturedDate = DateTime.UtcNow;
                    payment.IsApproved = true;
                    payment.Transactions.Add(new PaymentGatewayTransaction()
                    {
                        Note = $"Transaction Info {transactionId}, Invoice Number: {invoiceNumber}",
                        ResponseData = $"Account Number: {accountNumber}",
                        Status = responseReasonText,
                        ResponseCode = responseReasonCode,
                        CurrencyCode = payment.Currency.ToString(),
                        Amount = decimal.Parse(totalPrice, CultureInfo.InvariantCulture),
                        IsProcessed = true,
                        ProcessedDate = DateTime.UtcNow
                    });
                }
                else if (PaymentActionType == "Authorization/Capture")
                {
                    result.NewPaymentStatus = payment.PaymentStatus = PaymentStatus.Authorized;
                }

                result.OuterId = payment.OuterId = transactionId;
                payment.AuthorizedDate = DateTime.UtcNow;
                result.IsSuccess = true;
                result.ReturnUrl = string.Format("{0}/{1}", RemoveSchemeFromUrl(ThankYouPageUrl), order.Number);
            }

            return result;
        }

        public override ProcessPaymentRequestResult ProcessPayment(ProcessPaymentRequest request)
        {
            var result = AbstractTypeFactory<ProcessPaymentRequestResult>.TryCreateInstance();

            var payment = request.Payment as PaymentIn ?? throw new InvalidOperationException($"\"{nameof(request.Payment)}\" should not be null and of \"{nameof(PaymentIn)}\" type.");
            var order = request.Order as CustomerOrder ?? throw new InvalidOperationException($"\"{nameof(request.Order)}\" should not be null and of \"{nameof(CustomerOrder)}\" type.");
#pragma warning disable S1481 // Unused local variables should be removed
            // Need to check shop existence, though not using it
            var store = request.Store as Store ?? throw new InvalidOperationException($"\"{nameof(request.Store)}\" should not be null and of \"{nameof(Store)}\" type.");
#pragma warning restore S1481 // Unused local variables should be removed

            var sequence = new Random().Next(0, 1000).ToString();
            var timeStamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
            var currency = payment.Currency.ToString();

            var fingerprint = HmacMD5(TxnKey, ApiLogin + "^" + sequence + "^" + timeStamp + "^" + payment.Sum.ToString("F", CultureInfo.InvariantCulture) + "^" + currency);

            var confirmationUrl = string.Format("{0}/{1}", ConfirmationUrl, request.Order.Id);

            var checkoutform = string.Empty;

            checkoutform += string.Format("<form action='{0}' method='POST'>", GetAuthorizeNetUrl());

            //credit cart inputs for user
            checkoutform += string.Format("<p><div style='float:left;width:250px;'><label>Credit Card Number</label><div id = 'CreditCardNumber'>{0}</div></div>", CreateInput(false, "x_card_num", "", 28));
            checkoutform += string.Format("<div style='float:left;width:70px;'><label>Exp.</label><div id='CreditCardExpiration'>{0}</div></div>", CreateInput(false, "x_exp_date", "", 5, "placeholder='MMYY'"));
            checkoutform += string.Format("<div style='float:left;width:70px;'><label>CCV</label><div id='CCV'>{0}</div></div></p>", CreateInput(false, "x_card_code", "", 5));

            //
            checkoutform += CreateInput(true, "x_login", ApiLogin);
            checkoutform += CreateInput(true, "x_invoice_num", request.OrderId);
            checkoutform += CreateInput(true, "x_po_num", order.Number);
            checkoutform += CreateInput(true, "x_relay_response", "TRUE");
            checkoutform += CreateInput(true, "x_relay_url", confirmationUrl);

            ///Fingerprint and params for it
            checkoutform += CreateInput(true, "x_fp_sequence", sequence);
            checkoutform += CreateInput(true, "x_fp_timestamp", timeStamp);
            checkoutform += CreateInput(true, "x_fp_hash", fingerprint);
            checkoutform += CreateInput(true, "x_currency_code", currency);
            checkoutform += CreateInput(true, "x_amount", payment.Sum.ToString("F", CultureInfo.InvariantCulture));

            checkoutform += GetAuthOrCapture();

            // Add a Submit button
            checkoutform += "<div style='clear:both'></div><p><input type='submit' class='submit' value='Pay with Authorize.NET' /></p></form>";

            checkoutform += "</form>";

            result.HtmlForm = checkoutform;
            result.IsSuccess = true;
            result.NewPaymentStatus = payment.PaymentStatus = PaymentStatus.Pending;

            return result;
        }

        public override RefundPaymentRequestResult RefundProcessPayment(RefundPaymentRequest context)
        {
            throw new NotImplementedException();
        }

        public override ValidatePostProcessRequestResult ValidatePostProcessRequest(NameValueCollection queryString)
        {
            var result = AbstractTypeFactory<ValidatePostProcessRequestResult>.TryCreateInstance();

            if (queryString.AllKeys.Contains("x_split_tender_id") || queryString.AllKeys.Contains("x_trans_id"))
                result.OuterId = queryString["x_split_tender_id"] ?? queryString["x_trans_id"];
            else
                return result;

            if (queryString.AllKeys.Contains("x_invoice_num") &&
                queryString.AllKeys.Contains("x_auth_code") &&
                queryString.AllKeys.Contains("x_amount") &&
                queryString.AllKeys.Contains("x_response_code") &&
                queryString.AllKeys.Contains("x_response_reason_code") &&
                queryString.AllKeys.Contains("x_response_reason_text") &&
                queryString.AllKeys.Contains("x_method") &&
                queryString.AllKeys.Contains("x_SHA2_Hash"))
            {
                var dataString = GetDataString(queryString);
                var sha2 = HMACSHA512(SHA5Hash, dataString);
                if (!string.IsNullOrEmpty(queryString["x_SHA2_Hash"]) && !string.IsNullOrEmpty(sha2) && string.Equals(sha2, queryString["x_SHA2_Hash"], StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSuccess = true;
                }
            }

            return result;
        }

        public override VoidPaymentRequestResult VoidProcessPayment(VoidPaymentRequest request)
        {
            var result = AbstractTypeFactory<VoidPaymentRequestResult>.TryCreateInstance();

            var payment = request.Payment as PaymentIn ?? throw new InvalidOperationException($"\"{nameof(request.Payment)}\" should not be null and of \"{nameof(PaymentIn)}\" type.");

            if (payment.PaymentStatus == PaymentStatus.Cancelled)
            {
                var voidRequest = new VoidRequest(payment.OuterId);
                var gate = new Gateway(ApiLogin, TxnKey, true);

                var response = gate.Send(voidRequest);

                if (response.Approved)
                {
                    payment.IsCancelled = true;
                    result.IsSuccess = true;
                    result.NewPaymentStatus = payment.PaymentStatus = PaymentStatus.Voided;
                    payment.VoidedDate = payment.CancelledDate = DateTime.UtcNow;
                }
                else
                {
                    result.ErrorMessage = response.Message;
                }
            }
            else
            {
                throw new InvalidOperationException("Only authorized payments can be voided");
            }
            return result;
        }

        private string GetAuthOrCapture()
        {
            if (PaymentActionType == "Sale")
                return CreateInput(true, "x_type", "AUTH_CAPTURE");
            else if (PaymentActionType == "Authorization/Capture")
                return CreateInput(true, "x_type", "AUTH_ONLY");
            else
                throw new InvalidOperationException($@"PaymentActionType {PaymentActionType} is not available");
        }

        private string CreateInput(bool isHidden, string inputName, string inputValue, int maxLength = 0, string supplementaryFields = null)
        {
            var retVal = string.Empty;
            if (isHidden)
            {
                retVal = string.Format("<input type='hidden' name='{0}' id='{0}' value='{1}' />", inputName, inputValue);
            }
            else
            {
                retVal = string.Format("<input type='text' size='{0}' maxlength='{0}' name='{1}' id='{1}' value='{2}' {3} />", maxLength, inputName, inputValue, supplementaryFields);
            }

            return retVal;
        }

        private const string ApiVersion = "3.1";

        private string HmacMD5(string key, string value)
        {
            var encKey = (new ASCIIEncoding()).GetBytes(key);
            var encData = (new ASCIIEncoding()).GetBytes(value);

            // create a HMACMD5 object with the key set
            var myhmacMD5 = new HMACMD5(encKey);

            // calculate the hash (returns a byte array)
            var hash = myhmacMD5.ComputeHash(encData);

            // loop through the byte array and add append each piece to a string to obtain a hash string
            var fingerprint = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                fingerprint.Append(hash[i].ToString("x").PadLeft(2, '0'));
            }

            return fingerprint.ToString();
        }

        private string GetDataString(NameValueCollection queryString)
        {
            var parameters = new[] { "x_trans_id", "x_test_request", "x_response_code", "x_auth_code", "x_cvv2_resp_code", "x_cavv_response", "x_avs_code", "x_method", "x_account_number", "x_amount", "x_company", "x_first_name", "x_last_name", "x_address", "x_city", "x_stat", "x_zi", "x_countr", "x_phon", "x_fa", "x_emai", "x_ship_to_compan", "x_ship_to_first_nam", "x_ship_to_last_nam", "x_ship_to_addres", "x_ship_to_cit", "x_ship_to_stat", "x_ship_to_zi", "x_ship_to_countr", "x_invoice_num" };
            var dataString = new StringBuilder();

            foreach (var parameter in parameters)
            {
                dataString.Append($"^{queryString[parameter]}");
            }

            dataString.Append("^");
            return dataString.ToString();
        }

        private string HMACSHA512(string key, string textToHash)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key", "HMACSHA512: Parameter key cannot be empty.");
            if (string.IsNullOrEmpty(textToHash))
                throw new ArgumentNullException("textToHash", "HMACSHA512: Parameter textToHash cannot be empty.");
            if (key.Length % 2 != 0 || key.Trim().Length < 2)
            {
                throw new ArgumentException("HMACSHA512: Parameter key cannot be odd or less than 2 characters.", "key");
            }
            try
            {
                byte[] k = Enumerable.Range(0, key.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(key.Substring(x, 2), 16))
                    .ToArray();
                HMACSHA512 hmac = new HMACSHA512(k);
                byte[] HashedValue = hmac.ComputeHash((new System.Text.ASCIIEncoding()).GetBytes(textToHash));
                return BitConverter.ToString(HashedValue).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("HMACSHA512: " + ex.Message);
            }
        }

        private string GetAuthorizeNetUrl()
        {
            var retVal = string.Empty;
            if (Mode == "test")
            {
                retVal = "https://test.authorize.net/gateway/transact.dll";
            }
            else
            {
                retVal = "https://secure.authorize.net/gateway/transact.dll";
            }

            return retVal;
        }

        private static string RemoveSchemeFromUrl(string url)
        {
            var i = url.IndexOf("://");
            if (i > 0)
            {
                url = url.Substring(i + 1);
            }
            return url;
        }
    }
}
