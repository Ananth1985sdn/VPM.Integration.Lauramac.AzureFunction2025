using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response
{
    public class EntityInfo
    {
        public string EntityId { get; set; }
        public string EntityName { get; set; }
        public string EntityType { get; set; }
        public bool? IsActive { get; set; }
    }
}
