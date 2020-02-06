namespace VirtoCommerce.AuthorizeNet.Web
{
    public class AuthorizeNetSecureOptions
    {
        /// <summary>
        /// AuthorizeNET API Login ID
        /// </summary>
        public string ApiLogin { get; set; }
        /// <summary>
        /// AuthorizeNET API password
        /// </summary>
        public string TxnKey { get; set; }
        /// <summary>
        /// AuthorizeNET signature key
        /// </summary>
        public string SHA2Hash { get; set; }
    }
}
