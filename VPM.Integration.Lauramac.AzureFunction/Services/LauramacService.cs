using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;

namespace VPM.Integration.Lauramac.AzureFunction.Services
{
    internal class LauramacService : ILauramacService
    {
        public Task<string> SendLoanDataAsync(LoanRequest loanRequest)
        {
            throw new NotImplementedException();
        }

        public Task<string> SendLoanDocumentDataAsync(LoanDocumentRequest loanDocumentRequest)
        {
            throw new NotImplementedException();
        }
    }
}
