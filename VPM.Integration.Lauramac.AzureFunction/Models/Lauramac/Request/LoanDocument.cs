using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request
{
    public class LoanDocument
    {
        public string LoanID { get; set; }
        public string Filename { get; set; }
        public bool? isExternalDocument { get; set; }
        public string? ExternalDocumentLink { get; set; }
        public string? ExternalFileId { get; set; }
        public string? ExternalRequestId { get; set; }
    }
}
