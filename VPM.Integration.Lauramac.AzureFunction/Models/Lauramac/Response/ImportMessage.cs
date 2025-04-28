using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response
{
    public class ImportMessage
    {
        public int Queued { get; set; }
        public int IgnoredDuplicate { get; set; }
        public int Error { get; set; }
    }
}
