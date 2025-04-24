using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request
{
    public class FilterTerm
    {
        public string canonicalName { get; set; }
        public object value { get; set; }
        public string matchType { get; set; }
        public bool include { get; set; } = true;
        public string precision { get; set; }
    }
}
