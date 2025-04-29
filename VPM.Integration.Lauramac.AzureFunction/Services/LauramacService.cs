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
                var lauraMacUserName = Environment.GetEnvironmentVariable("LauraMacUsername");
                var lauraMacPassword = Environment.GetEnvironmentVariable("LauraMacPassword");
                var lauraMacBaseURL = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                var lauraMacTokenURL = Environment.GetEnvironmentVariable("LauraMacTokenURL");
                var fullUrl = $"{lauraMacBaseURL}{lauraMacTokenURL}";
                string accessToken = await GetLauramacAccessToken(lauraMacUserName, lauraMacPassword, fullUrl);               
               
                if (string.IsNullOrEmpty(accessToken) || accessToken.Contains("Error") || accessToken.Contains("Exception"))
                {
                    return new ImportResponse { Status = accessToken };
                }

                var importLoansUrl = Environment.GetEnvironmentVariable("LauraMacImportLoansUrl");
                var requestUrl = $"{lauraMacBaseURL}{importLoansUrl}";
                _logger.LogInformation("Request URL: {RequestUrl}", requestUrl);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string loanJson = JsonConvert.SerializeObject(loanRequest);
                var content = new StringContent(loanJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var importSuccessResponse = JsonConvert.DeserializeObject<ImportResponse>(responseContent);
                    if (importSuccessResponse != null)
                    {
                        importSuccessResponse.Status = "Success";
                        return importSuccessResponse;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error response received: {ErrorContent}", errorContent);
                var importErrorResponse = JsonConvert.DeserializeObject<ImportResponse>(errorContent);
               
                if (importErrorResponse != null)
                {
                    importErrorResponse.Status = "Failure";
                    return importErrorResponse;
                }

                return new ImportResponse { Status = "Failure" };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while posting loan data to SendLoanDataAsync");
                return new ImportResponse { Status = $"Exception: {ex.Message}" };
            }
        }
        public async Task<string> GetLauramacAccessToken(string username, string password, string fullUrl)
        {
            try
            {
                var lauraMacBaseURL = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                var importLoansUrl = Environment.GetEnvironmentVariable("LauraMacImportLoansUrl");
                var lauraMacUserName = Environment.GetEnvironmentVariable("LauraMacUsername");
                var lauraMacPassword = Environment.GetEnvironmentVariable("LauraMacPassword");

                var body = new
                {
                    username = lauraMacUserName,
                    password = lauraMacPassword
                };

                string jsonBody = JsonConvert.SerializeObject(body);
                HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(fullUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    return obj?.access_token;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lauramac token request failed. Status: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);
                    return $"Error: {response.StatusCode}, Content: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while requesting Lauramac access token.");
                return $"Exception: {ex.Message}";
            }
        }
        public async Task<List<DocumentUploadResult>> SendLoanDocumentDataAsync(LoanDocumentRequest loanDocumentRequest)
        {
            const int MaxRetries = 3;
            var finalResults = new List<DocumentUploadResult>();
            var remainingDocuments = loanDocumentRequest.LoanDocuments;

            try
            {
                var lauraMacUserName = Environment.GetEnvironmentVariable("LauraMacUsername");
                var lauraMacPassword = Environment.GetEnvironmentVariable("LauraMacPassword");
                var lauraMacBaseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
                var lauraMacTokenUrl = Environment.GetEnvironmentVariable("LauraMacTokenURL");
                var fullTokenUrl = $"{lauraMacBaseUrl}{lauraMacTokenUrl}";

                string accessToken = await GetLauramacAccessToken(lauraMacUserName, lauraMacPassword, fullTokenUrl);

                if (string.IsNullOrEmpty(accessToken) || accessToken.Contains("Error") || accessToken.Contains("Exception"))
                {
                    return finalResults;
                }

                var importLoanDocumentsUrl = Environment.GetEnvironmentVariable("LauraMacImportLoanDocumentsUrl");
                var requestUrl = $"{lauraMacBaseUrl}{importLoanDocumentsUrl}";
                _logger.LogInformation("Request URL: {RequestUrl}", requestUrl);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string loanJson = JsonConvert.SerializeObject(loanDocumentRequest);
                var content = new StringContent(loanJson, Encoding.UTF8, "application/json");

                HttpResponseMessage? response = null;

                for (int attempt = 1; attempt <= MaxRetries && remainingDocuments.Count > 0; attempt++)
                {
                    try
                    {
                        response = await _httpClient.PostAsync(requestUrl, content);
                    }
                    catch (HttpRequestException) when (attempt < MaxRetries)
                    {
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

                            remainingDocuments = remainingDocuments
                                .Where(doc =>
                                {
                                    var loanId = ((dynamic)doc).LoanID;
                                    return uploadResponse.Results.Any(r => r.LoanID == loanId && r.Status == "failure");
                                })
                                .ToList();
                        }
                        else
                        {
                            if (attempt == MaxRetries)
                            {
                                foreach (var doc in remainingDocuments)
                                {
                                    // var result = await RetrySingleDocumentAsync(_httpClient, doc, MaxRetries);
                                    // finalResults.Add(result);
                                }

                                break;
                            }

                            await Task.Delay(GetRetryDelay(attempt));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while posting loan documents data to SendLoanDocumentDataAsync");
            }

            return finalResults;
        }
        private int GetRetryDelay(int attempt)
        {
            return (int)(Math.Pow(2, attempt) * 500 + new Random().Next(100));
        }
    }
}
