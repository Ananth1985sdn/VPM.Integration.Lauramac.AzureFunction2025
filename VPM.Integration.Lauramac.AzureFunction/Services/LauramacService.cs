using System.Net.Http.Headers;
using System.Text;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response;

namespace VPM.Integration.Lauramac.AzureFunction.Services
{
    public class LauramacService : ILauramacService
    {
        private readonly ILogger<LauramacService> _logger;
        private readonly HttpClient _httpClient;

        public LauramacService(ILogger<LauramacService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<ImportResponse> SendLoanDataAsync(LoanRequest loanRequest)
        {
            try
            {
                if(string.IsNullOrEmpty(loanRequest.SellerName)) {
                    return new ImportResponse { Status = "SellerName is required." };
                }

                loanRequest.Loans = loanRequest.Loans != null
                                    ? loanRequest.Loans.Where(loan => !string.IsNullOrWhiteSpace(loan.LoanID)).ToList()
                                    : new List<Models.Lauramac.Request.Loan>();

                if (loanRequest.Loans == null || !loanRequest.Loans.Any())
                    return new ImportResponse { Status = "No valid loans to process." };



                string username = Environment.GetEnvironmentVariable("LauraMacUsername");
                string password = Environment.GetEnvironmentVariable("LauraMacPassword");
                string baseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                string tokenUrl = Environment.GetEnvironmentVariable("LauraMacTokenURL");

                string fullTokenUrl = $"{baseUrl}{tokenUrl}";
                string accessToken = await GetLauramacAccessToken(username, password, fullTokenUrl);

                if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Contains("Error") || accessToken.Contains("Exception"))
                    return new ImportResponse { Status = accessToken };

                string importLoansUrl = Environment.GetEnvironmentVariable("LauraMacImportLoansUrl");
                string requestUrl = $"{baseUrl}{importLoansUrl}";

                _logger.LogInformation("Sending loan data to URL: {RequestUrl}", requestUrl);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string jsonBody = JsonConvert.SerializeObject(loanRequest);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ImportResponse>(responseContent);
                    result.Status = "Success";
                    return result;
                }

                _logger.LogError("Loan import failed. Response: {ResponseContent}", responseContent);
                var errorResponse = JsonConvert.DeserializeObject<ImportResponse>(responseContent);
                errorResponse.Status = "Failure";
                return errorResponse ?? new ImportResponse { Status = "Failure" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendLoanDataAsync");
                return new ImportResponse { Status = $"Exception: {ex.Message}" };
            }
        }

        public async Task<string> GetLauramacAccessToken(string username, string password, string fullUrl)
        {
            try
            {
                var requestBody = new
                {
                    username,
                    password
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(fullUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic obj = JsonConvert.DeserializeObject(responseContent);
                    return obj?.access_token;
                }

                _logger.LogError("Token request failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, responseContent);
                return $"Error: {response.StatusCode}, Content: {responseContent}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GetLauramacAccessToken");
                return $"Exception: {ex.Message}";
            }
        }

        public async Task<List<DocumentUploadResult>> SendLoanDocumentDataAsync(DocumentUploadRequest documentUploadRequest)
        {
            const int MaxRetries = 3;
            var request = documentUploadRequest.LoanDocumentRequest;
            var finalResults = new List<DocumentUploadResult>();
            var remainingDocs = request.LoanDocuments;
            
            if (string.IsNullOrEmpty(request.SellerName)) {
                return finalResults;
            }

            request.LoanDocuments = request.LoanDocuments?
                .Where(doc => !string.IsNullOrWhiteSpace(doc.LoanID) && !string.IsNullOrWhiteSpace(doc.Filename))
                .ToList() ?? new List<LoanDocument>();

            if (!request.LoanDocuments.Any())
            {
                _logger.LogWarning("No valid loan documents to process.");
                return finalResults;
            }
            try
            {

                string username = Environment.GetEnvironmentVariable("LauraMacUsername");
                string password = Environment.GetEnvironmentVariable("LauraMacPassword");
                string baseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                string tokenUrl = Environment.GetEnvironmentVariable("LauraMacTokenURL");

                string fullTokenUrl = $"{baseUrl}{tokenUrl}";
                string accessToken = await GetLauramacAccessToken(username, password, fullTokenUrl);

                if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Contains("Error") || accessToken.Contains("Exception"))
                    return finalResults;

                string importUrl = Environment.GetEnvironmentVariable("LauraMacImportLoanDocumentsUrl");
                string requestUrl = $"{baseUrl}{importUrl}";

                _logger.LogInformation("Sending loan documents to URL: {RequestUrl}", requestUrl);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                _httpClient.DefaultRequestHeaders.Add("Username", documentUploadRequest.UserName);
                string jsonBody = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                for (int attempt = 1; attempt <= MaxRetries && remainingDocs.Count > 0; attempt++)
                {
                    HttpResponseMessage response = null;

                    try
                    {
                        response = await _httpClient.PostAsync(requestUrl, content);
                    }
                    catch (HttpRequestException ex) when (attempt < MaxRetries)
                    {
                        _logger.LogWarning(ex,
                        "HttpRequestException on attempt {Attempt}/{MaxRetries} to {RequestUrl}. Retrying after delay...",
                        attempt,
                        MaxRetries,
                        requestUrl);
                        await Task.Delay(GetRetryDelay(attempt));
                        continue;
                    }

                    if (response != null)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            var uploadResponse = JsonConvert.DeserializeObject<UploadResponse>(responseContent);
                            finalResults.AddRange(uploadResponse.Results);

                            remainingDocs = remainingDocs
                                .Where(doc =>
                                {
                                    var loanId = ((dynamic)doc).LoanID;
                                    return uploadResponse.Results.Any(r => r.LoanID == loanId && r.Status == "failure");
                                })
                                .ToList();
                        }
                        else if (attempt == MaxRetries)
                        {
                            foreach (var doc in remainingDocs)
                            {
                                var result = await RetrySingleDocumentAsync(doc, request.SellerName, request.TransactionIdentifier, requestUrl);
                                finalResults.Add(result);
                            }

                            break;
                        }

                        await Task.Delay(GetRetryDelay(attempt));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception in SendLoanDocumentDataAsync. SellerName: {SellerName}, TransactionIdentifier: {TransactionIdentifier}, RemainingDocuments: {RemainingDocumentsCount}",
                    request.SellerName,
                    request.TransactionIdentifier,
                    remainingDocs?.Count ?? 0);
            }

            return finalResults;
        }

        private async Task<DocumentUploadResult> RetrySingleDocumentAsync(
            LoanDocument document,
            string sellerName,
            string transactionIdentifier,
            string requestUrl)
        {
            const int MaxRetries = 3;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                var payload = new
                {
                    LoanDocuments = new List<LoanDocument> { document },
                    SellerName = sellerName,
                    TransactionIdentifier = transactionIdentifier
                };

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };

                try
                {
                    var response = await _httpClient.SendAsync(request);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<UploadResponse>(responseContent)?.Results?.FirstOrDefault();
                        if (result != null)
                            return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                     "Exception in RetrySingleDocumentAsync. Document: {Filename}, LoanID: {LoanID}, Attempt: {Attempt}",
                      document.Filename,
                      document.LoanID,
                      attempt);
                }

                await Task.Delay(GetRetryDelay(attempt));
            }

            return new DocumentUploadResult
            {
                LoanID = ((dynamic)document).LoanID,
                Status = "failure",
                ImportMessage = "Retry failed after max attempts"
            };
        }

        private int GetRetryDelay(int attempt)
        {
            return (int)(Math.Pow(2, attempt) * 500 + new Random().Next(100));
        }
    }
}
