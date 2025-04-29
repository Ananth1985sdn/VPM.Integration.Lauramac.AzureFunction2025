using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response
{
    public class ImportResponse
    {
        [JsonProperty("Import Message")]
        public ImportMessage ImportMessage { get; set; }

        public string? Status { get; set; }

        public List<Loan> Loans { get; set; }
    }
}
