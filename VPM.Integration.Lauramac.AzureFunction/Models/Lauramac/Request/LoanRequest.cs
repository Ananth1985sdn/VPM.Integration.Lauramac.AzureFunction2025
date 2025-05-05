using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request
{
    public class LoanRequest
    {
        [JsonProperty("Loans")]
        public List<Loan> Loans { get; set; }

        [JsonProperty("Transaction Identifier")]
        public string? TransactionIdentifier { get; set; }

        [JsonProperty("Seller Name")]
        public string SellerName { get; set; }

        private string _overrideDuplicateLoans = "0";

        [JsonProperty("overrideDuplicateLoans")]
        public string OverrideDuplicateLoans
        {
            get => _overrideDuplicateLoans;
            set => _overrideDuplicateLoans = string.IsNullOrWhiteSpace(value) ? "0" : value;
        }
    }
    }
}
