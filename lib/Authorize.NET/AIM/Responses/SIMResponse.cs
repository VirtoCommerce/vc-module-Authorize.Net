using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Web;

namespace AuthorizeNet {
    public class SIMResponse : AuthorizeNet.IGatewayResponse {

        NameValueCollection _post;
        string _merchantHash;
        public SIMResponse(NameValueCollection post) {
            _post = post;
        }


        public string MD5Hash {
            get {
                return FindKey("x_MD5_Hash");
            }
        }

        public string ResponseCode {
            get {
                return FindKey("x_response_code");
            }
        }

        public string ResponseReasonCode
        {
            get {
                return FindKey("x_response_reason_code");
            }
        }

        public string Message {
            get {
                return FindKey("x_response_reason_text");
            }
        }

        public bool Approved {
            get {
                return this.ResponseCode == "1";
            }
        }

        public string InvoiceNumber {
            get {
                return FindKey(ApiFields.InvoiceNumber);
            }
        }

        public decimal Amount {
            get {
                var sAmount =  FindKey(ApiFields.Amount);
                decimal result = 0.00M;
                decimal.TryParse(sAmount, out result);
                return result;
            }
        }

        public string TransactionID {
            get {
                return FindKey(ApiFields.TransactionID);
            }
        }

        public string AuthorizationCode {
            get {
                return FindKey(ApiFields.AuthorizationCode);
            }
        }

        public string CardNumber {
            get {
                return FindKey(ApiFields.CreditCardNumber);
            }
        }

        public string CardType
        {
            get { return FindKey(ApiFields.CreditCardType); }
        }

        public string GetValue(string name)
        {
            return FindKey(name);
        }

        string FindKey(string key) {
            string result = null;

            if (_post[key] != null) {
                result = _post[key];
            }

            return result;
        }

        public string GetValueByIndex(int position)
        {
            return ParseResponse(position);
        }

        internal string ParseResponse(int index)
        {
            var result = "";
            if (_post.AllKeys.Count() > index)
            {
                result = _post[index];
            }
            return result;
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.AppendFormat("<li>Code = {0}", this.ResponseCode);
            sb.AppendFormat("<li>Auth = {0}", this.AuthorizationCode);
            sb.AppendFormat("<li>Message = {0}", this.Message);
            sb.AppendFormat("<li>TransID = {0}", this.TransactionID);
            sb.AppendFormat("<li>Approved = {0}", this.Approved);
            return sb.ToString();
        }
    }
}
