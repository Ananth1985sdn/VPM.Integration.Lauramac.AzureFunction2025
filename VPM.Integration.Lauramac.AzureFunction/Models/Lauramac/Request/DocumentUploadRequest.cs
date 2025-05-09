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
        public List<LoanDocumentRequest> LoanDocumentRequest { get; set; }
    }
}
