using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;

namespace VPM.Integration.Lauramac.AzureFunction.Interface
{
    public interface ILauramacService
    {
        Task<string> GetLauramacAccessToken(string username, string password, string fullUrl);
        Task<string> SendLoanDataAsync(LoanRequest loanRequest);
        Task<string> SendLoanDocumentDataAsync(LoanDocumentRequest loanDocumentRequest);
    }
}
