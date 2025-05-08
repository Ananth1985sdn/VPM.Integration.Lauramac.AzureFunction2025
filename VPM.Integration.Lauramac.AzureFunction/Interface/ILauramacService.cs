using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response;

namespace VPM.Integration.Lauramac.AzureFunction.Interface
{
    public interface ILauramacService
    {
        Task<string> GetLauramacAccessToken(string username, string password, string fullUrl);
        Task<ImportResponse> SendLoanDataAsync(LoanRequest loanRequest);
        Task<List<DocumentUploadResult>> SendLoanDocumentDataAsync(DocumentUploadRequest loanDocumentRequest);
    }
}
