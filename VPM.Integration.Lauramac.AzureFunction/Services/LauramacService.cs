using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
        public async Task<string> SendLoanDataAsync(LoanRequest loanRequest)
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
                    return null;
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
                    return await response.Content.ReadAsStringAsync();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error response received: {ErrorContent}", errorContent);
                return $"Error: {response.StatusCode}, Content: {errorContent}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while posting loan data to SendLoanDataAsync");
                return $"Exception: {ex.Message}";
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
        public Task<string> SendLoanDocumentDataAsync(LoanDocumentRequest loanDocumentRequest)
        {
            throw new NotImplementedException();
        }
    }
}
