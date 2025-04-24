using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPM.Integration.Lauramac.AzureFunction.Interface
{
    public interface ILoanDataService
    {
        public Task<string> GetToken(string username, string password, string clientId, string clientSecret, string fullUrl);
        public Task<string> GetLoanData(string requestUrl, StringContent content, string accessToken);
        public Task<string> GetAllLoanDocuments(string accessToken, string loanId);
        public Task<string> GetDocumentUrl(string loanId, string attachmentId, string accessToken);
        public Task<bool> DownloadDocument(string loanId, string lastName, string documentURL);
    }
}
