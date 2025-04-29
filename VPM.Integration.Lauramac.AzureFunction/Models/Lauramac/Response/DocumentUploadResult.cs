using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response
{
    public class DocumentUploadResult
    {
        public string ExternalFileId { get; set; }
        public string Status { get; set; } 
        public string Message { get; set; }
    }
}
