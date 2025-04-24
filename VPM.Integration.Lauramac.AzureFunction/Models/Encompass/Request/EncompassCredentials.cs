using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request
{
    public class EncompassCredentials
    {
        public string? ClientId { get; }
        public string? ClientSecret { get; }
        public string? Username { get; }
        public string? Password { get; }

        public EncompassCredentials()
        {
            ClientId = Environment.GetEnvironmentVariable("EncompassClientId");
            ClientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");
            Username = Environment.GetEnvironmentVariable("EncompassUsername");
            Password = Environment.GetEnvironmentVariable("EncompassPassword");
        }
    }
}
