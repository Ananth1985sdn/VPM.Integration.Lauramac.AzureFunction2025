using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request
{
    public class DocumentUploadRequest
    {
        public string File { get; set; }
        public LoanDocumentRequest LoanDocumentRequest { get; set; }
    }
}
