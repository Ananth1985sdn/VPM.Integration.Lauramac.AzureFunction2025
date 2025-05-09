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
                if (string.IsNullOrEmpty(loanRequest.SellerName))
                {
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
            var finalResults = new List<DocumentUploadResult>();

            if (documentUploadRequest.LoanDocumentRequest == null || !documentUploadRequest.LoanDocumentRequest.Any())
            {
                _logger.LogError("SendLoanDocumentDataAsync: No loan documents to process.");
                return finalResults;
            }

            try
            {
                string username = Environment.GetEnvironmentVariable("LauraMacUsername");
                string password = Environment.GetEnvironmentVariable("LauraMacPassword");
                string baseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                string tokenUrl = Environment.GetEnvironmentVariable("LauraMacTokenURL");
                string importUrl = Environment.GetEnvironmentVariable("LauraMacImportLoanDocumentsUrl");

                string requestUrl = $"{baseUrl}{importUrl}";
                string fullTokenUrl = $"{baseUrl}{tokenUrl}";

                _logger.LogInformation("SendLoanDocumentDataAsync: Requesting access token.");
                string accessToken = await GetLauramacAccessToken(username, password, fullTokenUrl);

                if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Contains("Error") || accessToken.Contains("Exception"))
                {
                    _logger.LogError("SendLoanDocumentDataAsync: Failed to retrieve a valid access token.");
                    return finalResults;
                }

                foreach (var loanDocumentRequest in documentUploadRequest.LoanDocumentRequest)
                {
                    string loanId = loanDocumentRequest?.LoanDocuments?.FirstOrDefault()?.LoanID ?? "Unknown";
                    string fileName = loanDocumentRequest?.LoanDocuments?.FirstOrDefault()?.Filename ?? "Unknown";

                    try
                    {
                        _logger.LogInformation("SendLoanDocumentDataAsync: Sending loan documents for LoanID: {LoanId}, FileName: {FileName} to URL: {Url}",
                            loanId, fileName, requestUrl);

                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        _httpClient.DefaultRequestHeaders.Remove("Username");
                        _httpClient.DefaultRequestHeaders.Add("Username", username);

                        string jsonBody = JsonConvert.SerializeObject(loanDocumentRequest);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        for (int attempt = 1; attempt <= MaxRetries; attempt++)
                        {
                            try
                            {
                                var response = await _httpClient.PostAsync(requestUrl, content);
                                var responseContent = await response.Content.ReadAsStringAsync();

                                if (response.IsSuccessStatusCode)
                                {
                                    var uploadResponse = JsonConvert.DeserializeObject<List<DocumentUploadResult>>(responseContent);
                                    if (uploadResponse != null)
                                    {
                                        finalResults.AddRange(uploadResponse);
                                        _logger.LogInformation("SendLoanDocumentDataAsync: Upload successful for LoanID: {LoanId}, FileName: {FileName}.", loanId, fileName);
                                    }
                                    break;
                                }
                                else
                                {
                                    _logger.LogWarning("SendLoanDocumentDataAsync: Upload failed for LoanID: {LoanId}, FileName: {FileName}. Response: {Response}",
                                        loanId, fileName, responseContent);

                                    var errorResponse = JsonConvert.DeserializeObject<List<DocumentUploadResult>>(responseContent);
                                    if (errorResponse != null)
                                    {
                                        finalResults.AddRange(errorResponse);
                                    }
                                    break;
                                }
                            }
                            catch (Exception ex) when (attempt < MaxRetries)
                            {
                                _logger.LogWarning(ex, "SendLoanDocumentDataAsync: Attempt {Attempt}/{MaxRetries} failed for LoanID: {LoanId}, FileName: {FileName}. Retrying...",
                                    attempt, MaxRetries, loanId, fileName);
                                await Task.Delay(GetRetryDelay(attempt));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SendLoanDocumentDataAsync: Exception while processing LoanID: {LoanId}, FileName: {FileName}", loanId, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendLoanDocumentDataAsync: Unexpected error.");
            }

            _logger.LogInformation("SendLoanDocumentDataAsync: Completed with {Count} results.", finalResults.Count);
            return finalResults;
        }
        private int GetRetryDelay(int attempt)
        {
            return (int)(Math.Pow(2, attempt) * 500 + new Random().Next(100));
        }
    }
}
