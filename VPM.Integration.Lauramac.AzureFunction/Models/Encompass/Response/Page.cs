using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class Page
    {
        public ImageMetadata PageImage { get; set; }
        public ImageMetadata ThumbnailImage { get; set; }
        public long FileSize { get; set; }
        public int Rotation { get; set; }
        public string originalKey { get; set; }
    }
}
