using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class Loan
    {
        [JsonProperty("loanId")]
        public string LoanId { get; set; }
        [JsonProperty("fields")]
        public LoanFields Fields { get; set; }
    }
}
