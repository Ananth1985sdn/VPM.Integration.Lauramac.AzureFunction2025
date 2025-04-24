using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class AttachmentDownloadUrlResponse
    {
        [JsonProperty("attachments")]
        public List<AttachmentDownloadUrl> AttachmentUrls { get; set; }
    }
}
