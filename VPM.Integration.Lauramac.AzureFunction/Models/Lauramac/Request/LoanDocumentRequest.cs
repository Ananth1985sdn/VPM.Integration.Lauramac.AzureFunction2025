using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request
{
    public class LoanDocumentRequest
    {
        [JsonProperty("LoanDocuments")]
        public List<LoanDocument> LoanDocuments { get; set; }

        [JsonProperty("Transaction Identifier")]
        public string? TransactionIdentifier { get; set; }

        [JsonProperty("Seller Name")]
        public string SellerName { get; set; }
    }
}
