using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class AttachmentDownloadUrl
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("pages")]
        public List<ImageItem> Pages { get; set; }
        public List<string> originalUrls { get; set; }

    }
}
