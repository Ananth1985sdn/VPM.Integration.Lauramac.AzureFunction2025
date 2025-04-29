using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response
{
    public class DocumentUploadResult
    {
        public string LoanID { get; set; }
        public string LoanUUID { get; set; }
        [JsonProperty("Import Message")]
        public string ImportMessage { get; set; }
        public string filename { get; set; }
        public string Status { get; set; }
        public string FileUUID { get; set; }
        public string? zipId { get; set; }
    }
}
