﻿using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AuthorizeNet;
using VirtoCommerce.Domain.Order.Model;
using VirtoCommerce.Domain.Payment.Model;

namespace Authorize.Net.Managers
{
    public class AuthorizeNetMethod : PaymentMethod
    {
        private const string _apiLoginStoreSetting = "AuthorizeNet.ApiLogin";
        private const string _txnKeyStoreSetting = "AuthorizeNet.TxnKey";
        private const string _confirmationUrlStoreSetting = "AuthorizeNet.ConfirmationUrl";
        private const string _thankYouPageRelativeUrlStoreSetting = "AuthorizeNet.ThankYouPageRelativeUrl";
        private const string _paymentActionTypeStoreSetting = "AuthorizeNet.PaymentActionType";
        private const string _modeStoreSetting = "AuthorizeNet.Mode";
        private const string _sha2HashStoreSetting = "AuthorizeNet.SHA2Hash";

        public AuthorizeNetMethod() : base("AuthorizeNet") { }

        public string ApiLogin
        {
            get
            {
                return GetSetting(_apiLoginStoreSetting);
            }
        }

        public string TxnKey
        {
            get
            {
                return GetSetting(_txnKeyStoreSetting);
            }
        }

        public string SHA5Hash
        {
            get
            {
                return GetSetting(_sha2HashStoreSetting);
            }
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

        private string ConfirmationUrl
        {
            get
            {
                return GetSetting(_confirmationUrlStoreSetting);
            }
        }

        private string ThankYouPageRelativeUrl
        {
            get
            {
                return GetSetting(_thankYouPageRelativeUrlStoreSetting);
            }
        }

        private string PaymentActionType
        {
            get
            {
                return GetSetting(_paymentActionTypeStoreSetting);
            }
        }

        private string Mode
        {
            get
            {
                return GetSetting(_modeStoreSetting);
            }
        }

        public override CaptureProcessPaymentResult CaptureProcessPayment(CaptureProcessPaymentEvaluationContext context)
        {
            var retVal = new CaptureProcessPaymentResult();

            var webClient = new WebClient();
            var form = new NameValueCollection();
            form.Add("x_login", ApiLogin);
            form.Add("x_tran_key", TxnKey);

            form.Add("x_delim_data", "TRUE");
            form.Add("x_delim_char", "|");
            form.Add("x_encap_char", "");
            form.Add("x_version", GetApiVersion());
            form.Add("x_method", "CC");
            form.Add("x_currency_code", context.Payment.Currency.ToString());
            form.Add("x_type", "CAPTURE_ONLY");

            var orderTotal = Math.Round(context.Payment.Sum, 2);
            form.Add("x_amount", orderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //x_trans_id. When x_test_request (sandbox) is set to a positive response, 
            //or when Test mode is enabled on the payment gateway, this value will be "0".
            form.Add("x_trans_id", context.Payment.OuterId);

            var responseData = webClient.UploadValues(GetAuthorizeNetUrl(), form);
            var reply = Encoding.ASCII.GetString(responseData);

            if (!string.IsNullOrEmpty(reply))
            {
                string[] responseFields = reply.Split('|');
                switch (responseFields[0])
                {
                    case "1":
                        retVal.NewPaymentStatus = context.Payment.PaymentStatus = PaymentStatus.Paid;
                        context.Payment.CapturedDate = DateTime.UtcNow;
                        retVal.OuterId = context.Payment.OuterId = string.Format("{0},{1}", responseFields[6], responseFields[4]);
                        retVal.IsSuccess = true;
                        context.Payment.IsApproved = true;
                        break;
                    case "2":
                        throw new NullReferenceException(string.Format("Declined ({0}: {1})", responseFields[2], responseFields[3]));
                    case "3":
                        throw new NullReferenceException(string.Format("Error: {0}", reply));
                }
            }
            else
            {
                throw new NullReferenceException("Authorize.NET unknown error");
            }

            return retVal;
        }

        public override PostProcessPaymentResult PostProcessPayment(PostProcessPaymentEvaluationContext context)
        {
            var retVal = new PostProcessPaymentResult();

            var transactionId = context.Parameters["x_split_tender_id"] ?? context.Parameters["x_trans_id"];
            var invoiceNumber = context.Parameters["x_invoice_num"];
            var authorizationCode = context.Parameters["x_auth_code"];
            var totalPrice = context.Parameters["x_amount"];
            var responseCode = context.Parameters["x_response_code"];
            var responseReasonCode = context.Parameters["x_response_reason_code"];
            var responseReasonText = context.Parameters["x_response_reason_text"];
            var method = context.Parameters["x_method"];
            var hash = context.Parameters["x_SHA2_Hash"];
            var accountNumber = context.Parameters["x_account_number"];

            var dataString = GetDataString(context.Parameters);
            var sha2 = HMACSHA512(SHA5Hash, dataString);

            if (!string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(sha2) && string.Equals(sha2, hash, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(responseCode) && responseCode.Equals("1"))
            {
                if (PaymentActionType == "Sale")
                {
                    retVal.NewPaymentStatus = context.Payment.PaymentStatus = PaymentStatus.Paid;
                    context.Payment.Status = PaymentStatus.Paid.ToString();
                    context.Payment.CapturedDate = DateTime.UtcNow;
                    context.Payment.IsApproved = true;
                    context.Payment.Transactions.Add(new PaymentGatewayTransaction()
                    {
                        Note = $"Transaction Info {transactionId}, Invoice Number: {invoiceNumber}",
                        ResponseData = $"Account Number: {accountNumber}",
                        Status = responseReasonText,
                        ResponseCode = responseReasonCode,
                        CurrencyCode = context.Payment.Currency.ToString(),
                        Amount = decimal.Parse(totalPrice, CultureInfo.InvariantCulture),
                        IsProcessed = true,
                        ProcessedDate = DateTime.UtcNow
                    });
                }
                else if (PaymentActionType == "Authorization/Capture")
                {
                    retVal.NewPaymentStatus = context.Payment.PaymentStatus = PaymentStatus.Authorized;
                }

                retVal.OuterId = context.Payment.OuterId = transactionId;
                context.Payment.AuthorizedDate = DateTime.UtcNow;
                retVal.IsSuccess = true;
                retVal.ReturnUrl = string.Format("{0}/{1}/{2}", context.Store.Url, ThankYouPageRelativeUrl, context.Order.Number);
            }

            return retVal;
        }

        public override ProcessPaymentResult ProcessPayment(ProcessPaymentEvaluationContext context)
        {
            var retVal = new ProcessPaymentResult();

            if (context.Order != null && context.Store != null && context.Payment != null)
            {
                var sequence = new Random().Next(0, 1000).ToString();
                var timeStamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                var currency = context.Payment.Currency.ToString();

                var fingerprint = HmacMD5(TxnKey, ApiLogin + "^" + sequence + "^" + timeStamp + "^" + context.Payment.Sum.ToString("F", CultureInfo.InvariantCulture) + "^" + currency);

                var confirmationUrl = string.Format("{0}/{1}", ConfirmationUrl, context.Order.Id);

                var checkoutform = string.Empty;

                checkoutform += string.Format("<form action='{0}' method='POST'>", GetAuthorizeNetUrl());

                //credit cart inputs for user
                checkoutform += string.Format("<p><div style='float:left;width:250px;'><label>Credit Card Number</label><div id = 'CreditCardNumber'>{0}</div></div>", CreateInput(false, "x_card_num", "", 28));
                checkoutform += string.Format("<div style='float:left;width:70px;'><label>Exp.</label><div id='CreditCardExpiration'>{0}</div></div>", CreateInput(false, "x_exp_date", "", 5, "placeholder='MMYY'"));
                checkoutform += string.Format("<div style='float:left;width:70px;'><label>CCV</label><div id='CCV'>{0}</div></div></p>", CreateInput(false, "x_card_code", "", 5));

                //
                checkoutform += CreateInput(true, "x_login", ApiLogin);
                checkoutform += CreateInput(true, "x_invoice_num", context.Order.Id);
                checkoutform += CreateInput(true, "x_po_num", context.Order.Number);
                checkoutform += CreateInput(true, "x_relay_response", "TRUE");
                checkoutform += CreateInput(true, "x_relay_url", confirmationUrl);

                ///Fingerprint and params for it
                checkoutform += CreateInput(true, "x_fp_sequence", sequence);
                checkoutform += CreateInput(true, "x_fp_timestamp", timeStamp);
                checkoutform += CreateInput(true, "x_fp_hash", fingerprint);
                checkoutform += CreateInput(true, "x_currency_code", currency);
                checkoutform += CreateInput(true, "x_amount", context.Payment.Sum.ToString("F", CultureInfo.InvariantCulture));

                checkoutform += GetAuthOrCapture();

                // Add a Submit button
                checkoutform += "<div style='clear:both'></div><p><input type='submit' class='submit' value='Pay with Authorize.NET' /></p></form>";

                checkoutform = checkoutform + DPMFormGenerator.EndForm();

                retVal.HtmlForm = checkoutform;
                retVal.IsSuccess = true;
                retVal.NewPaymentStatus = context.Payment.PaymentStatus = PaymentStatus.Pending;
            }

            return retVal;
        }

        public override RefundProcessPaymentResult RefundProcessPayment(RefundProcessPaymentEvaluationContext context)
        {
            throw new NotImplementedException();
        }

        public override ValidatePostProcessRequestResult ValidatePostProcessRequest(NameValueCollection queryString)
        {
            var retVal = new ValidatePostProcessRequestResult();

            if (queryString.AllKeys.Contains("x_split_tender_id") || queryString.AllKeys.Contains("x_trans_id"))
                retVal.OuterId = queryString["x_split_tender_id"] ?? queryString["x_trans_id"];
            else
                return retVal;

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
                    retVal.IsSuccess = true;
                }
            }

            return retVal;
        }

        public override VoidProcessPaymentResult VoidProcessPayment(VoidProcessPaymentEvaluationContext context)
        {
            var retVal = new VoidProcessPaymentResult();

            if (context.Payment.PaymentStatus == PaymentStatus.Cancelled)
            {
                var request = new VoidRequest(context.Payment.OuterId);
                var gate = new Gateway(ApiLogin, TxnKey, true);

                var response = gate.Send(request);

                if (response.Approved)
                {
                    context.Payment.IsCancelled = true;
                    retVal.IsSuccess = true;
                    retVal.NewPaymentStatus = context.Payment.PaymentStatus = PaymentStatus.Voided;
                    context.Payment.VoidedDate = context.Payment.CancelledDate = DateTime.UtcNow;
                }
                else
                {
                    retVal.ErrorMessage = response.Message;
                }
            }
            else
            {
                throw new NullReferenceException("Only authorized payments can be voided");
            }
            return retVal;
        }

        private string GetAuthOrCapture()
        {
            if (PaymentActionType == "Sale")
                return CreateInput(true, "x_type", "AUTH_CAPTURE");
            else if (PaymentActionType == "Authorization/Capture")
                return CreateInput(true, "x_type", "AUTH_ONLY");
            else
                throw new NullReferenceException("PaymentActionType is not available");
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

        private string GetApiVersion()
        {
            return "3.1";
        }

        private string HmacMD5(string key, string value)
        {
            byte[] encKey = (new ASCIIEncoding()).GetBytes(key);
            byte[] encData = (new ASCIIEncoding()).GetBytes(value);

            // create a HMACMD5 object with the key set
            HMACMD5 myhmacMD5 = new HMACMD5(encKey);

            // calculate the hash (returns a byte array)
            byte[] hash = myhmacMD5.ComputeHash(encData);

            // loop through the byte array and add append each piece to a string to obtain a hash string
            string fingerprint = string.Empty;
            for (int i = 0; i < hash.Length; i++)
            {
                fingerprint += hash[i].ToString("x").PadLeft(2, '0');
            }

            return fingerprint;
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
                throw new ArgumentNullException("HMACSHA512: key", "Parameter cannot be empty.");
            if (string.IsNullOrEmpty(textToHash))
                throw new ArgumentNullException("HMACSHA512: textToHash", "Parameter cannot be empty.");
            if (key.Length % 2 != 0 || key.Trim().Length < 2)
            {
                throw new ArgumentNullException("HMACSHA512: key", "Parameter cannot be odd or less than 2 characters.");
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
                throw new Exception("HMACSHA512: " + ex.Message);
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
    }
}