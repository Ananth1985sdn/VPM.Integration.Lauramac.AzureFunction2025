using System;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request;
using Newtonsoft.Json;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response;

using EncompassLoan = VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response.Loan;
using LauramacLoan = VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request.Loan;
using Newtonsoft.Json.Linq;

namespace VPM.Integration.Lauramac.AzureFunction
{
    public class LauramacAzureFunction
    {
        private readonly ILogger _logger;
        private readonly ILoanDataService _loanDataService;

        public LauramacAzureFunction(ILoggerFactory loggerFactory, ILoanDataService loanDataService)
        {
            _logger = loggerFactory.CreateLogger<LauramacAzureFunction>();
            _loanDataService = loanDataService;
        }

        [Function("LauramacAzureFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            try
            {
                // Your function logic here
                _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
                string token = await GetEncompassAccessTokenAsync(_logger);

                if (myTimer.ScheduleStatus is not null)
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        await CallLoanPipelineApiAsync(token);
                    }
                    else
                    {
                        _logger.LogError("Failed to retrieve Encompass access token.");
                    }
                    _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }

        public async Task<string> GetEncompassAccessTokenAsync(ILogger log)
        {
            var encompassBaseURL = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var tokenUrl = Environment.GetEnvironmentVariable("EncompassTokenUrl");

            var credentials = new EncompassCredentials();
            var clientId = credentials.ClientId;
            var clientSecret = credentials.ClientSecret;
            var username = credentials.Username;
            var password = credentials.Password;

            var fullUrl = $"{encompassBaseURL.TrimEnd('/')}{tokenUrl}";

            string token = await _loanDataService.GetToken(username, password, clientId, clientSecret, fullUrl);
            return token;
        }

        private async Task CallLoanPipelineApiAsync(string token)
        {
            var encompassBaseURL = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var pipeLineUrl = Environment.GetEnvironmentVariable("EncompassLoanPipelineURL");
            var requestUrl = $"{encompassBaseURL.TrimEnd('/')}{pipeLineUrl}";
            var documentPackage = Environment.GetEnvironmentVariable("DocumentPackageName");

            var requestBody = RequestBody();

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _loanDataService.GetLoanData(requestUrl, content, token);
            _logger.LogInformation("Loan Pipeline Response: " + response);

            if (response != null && response != "[]")
            {
                try
                {
                    var parsedJson = JToken.Parse(response);
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    _logger.LogInformation("Invalid JSON in Loan Pipeline Response: {Response}. Error: {ErrorMessage}", response, ex.Message);
                    return;
                }
                try
                {
                    var loans = JsonConvert.DeserializeObject<List<EncompassLoan>>(response);
                    _logger.LogInformation($"Number of Loans: {loans.Count}");

                    var baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
                    var endpointTemplate = Environment.GetEnvironmentVariable("EncompassGetDocumentsURL");

                    LoanRequest loanRequest = new LoanRequest();
                    loanRequest.Loans = new List<LauramacLoan>();
                    string sellerName = string.Empty;

                    foreach (var loan in loans)
                    {
                        sellerName = string.IsNullOrEmpty(sellerName) ? loan.Fields.FieldsCXNAME_DDPROVIDER : sellerName;
                        _logger.LogInformation($"Loan ID: {loan.LoanId}, Loan Number: {loan.Fields.LoanNumber}, Amount: {loan.Fields.LoanAmount}");

                        var documentsResponse = await _loanDataService.GetAllLoanDocuments(token, loan.LoanId);

                        try
                        {
                            var parsedJson = JToken.Parse(documentsResponse);
                        }
                        catch (Newtonsoft.Json.JsonReaderException ex)
                        {
                            _logger.LogInformation("Invalid JSON in Loan Documents Response: {Response}. Error: {ErrorMessage}", documentsResponse, ex.Message);
                            continue;
                        }

                        _logger.LogInformation($"Attachments for Loan {loan.Fields.LoanNumber}: {documentsResponse}");

                        List<Attachment> attachments = new();

                        if (documentsResponse != null && documentsResponse != "[]")
                        {
                            try
                            {
                                attachments = JsonConvert.DeserializeObject<List<Attachment>>(documentsResponse) ?? new List<Attachment>();
                            }
                            catch (Newtonsoft.Json.JsonException ex)
                            {
                                _logger.LogError($"Error deserializing response: {ex.Message}");
                            }

                            foreach (var attachment in attachments ?? Enumerable.Empty<Attachment>())
                            {
                                if (attachment.AssignedTo?.EntityName != documentPackage || (attachment.FileSize <= 0 || attachment.Type != "Image"))
                                    continue;
                                else
                                {
                                    _logger.LogInformation($"Attachment Title: {attachment.Title}, CreatedBy: {attachment.AssignedTo?.EntityName}, File Size: {attachment.FileSize}");
                                    loan.LoanId = "66b6fc88-f675-4cdd-b78a-214453cde1e9";
                                    attachment.Id = "eb00e165-4ce6-4580-a39a-555067afdaca";
                                    var url = await _loanDataService.GetDocumentUrl(loan.LoanId, attachment.Id, token);
                                    if (url != null)
                                    {
                                        var documentDownload = await _loanDataService.DownloadDocument(loan.LoanId, loan.Fields.Field4002, url);
                                        if (documentDownload)
                                        {
                                            var lauramacLoan = new Models.Lauramac.Request.Loan
                                            {
                                                LoanID = loan.LoanId,
                                                LoanNumber = loan.Fields.LoanNumber,
                                                LoanAmount = loan.Fields.LoanAmount,
                                                NoteRate = loan.Fields.Field3,
                                                LoanTerm = loan.Fields.Field325,
                                                Purpose = loan.Fields.Field19,
                                                Fico = loan.Fields.CreditScore,
                                                OriginalLTV = loan.Fields.LTV,
                                                OriginalCLTV = loan.Fields.Field976,
                                                AppraisedValue = loan.Fields.Field356,
                                                PurchasePrice = loan.Fields.FieldsCXPURCHASEPRICE,
                                                DocType = loan.Fields.DocType,
                                                AmortizationType = loan.Fields.Field608,
                                                PropType = loan.Fields.Field1401,
                                                Occupancy = loan.Fields.OccupancyStatus,

                                                // Borrower Info
                                                BorrowerFirstName = loan.Fields.Field4000,
                                                BorrowerLastName = loan.Fields.Field4002,
                                                BorrowerSSN = loan.Fields.Field65,

                                                // Property Info
                                                Address = loan.Fields.Address1,
                                                City = loan.Fields.City,
                                                State = loan.Fields.State,
                                                Zip = loan.Fields.Field15
                                            };

                                            loanRequest.Loans.Add(lauramacLoan);
                                            break;
                                        }

                                    }
                                }
                            }
                        }
                    }
                    loanRequest.TransactionIdentifier = "VPM WHSL_2025-02-VPM";
                    loanRequest.SellerName = sellerName;
                    loanRequest.OverrideDuplicateLoans = "0";
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    _logger.LogError($"Error deserializing response: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation("No loans match the given criteria.");
            }
        }

        private static Object RequestBody()
        {
            var filterTerms = new List<FilterTerm>
            {
                new FilterTerm {
                    canonicalName = "Loan.CurrentMilestoneName",
                    value = new[] { "Started" },
                    matchType = "MultiValue",
                    include = true
                },
                new FilterTerm {
                    canonicalName = "Loan.LoanNumber",
                    value = "5",
                    matchType = "startsWith",
                    include = false
                },
                new FilterTerm {
                    canonicalName = "Fields.CX.DUEDILIGENCE_START_DT",
                    value = "04/11/2025",
                    matchType = "Equals",
                    precision = "Day"
                },
                new FilterTerm {
                    canonicalName = "Fields.CX.NAME_DDPROVIDER",
                    value = "Canopy",
                    matchType = "Exact",
                    include = true
                }
            };

            var requestBody = new
            {
                fields = new[]
                {
                    "Loan.LoanNumber", "Fields.19", "Fields.608", "Loan.LoanAmount", "Loan.LTV", "Fields.976",
                    "Loan.Address1", "Loan.City", "Loan.State", "Fields.15", "Fields.1041", "Loan.OccupancyStatus",
                    "Fields.1401", "Fields.CX.VP.DOC.TYPE", "Fields.4000", "Fields.4002", "Fields.CX.CREDITSCORE",
                    "Fields.325", "Fields.3", "Fields.742", "Fields.CX.VP.BUSINESS.PURPOSE", "Fields.1550",
                    "Fields.675", "Fields.QM.X23", "Fields.QM.X25", "Fields.2278", "Fields.65","Fields.CX.PURCHASEPRICE","Fields.1550","Fields.356","Fields.CX.NAME_DDPROVIDER"
                },
                filter = new
                {
                    @operator = "and",
                    terms = filterTerms
                },
                orgType = "internal",
                loanOwnership = "AllLoans",
                sortOrder = new[]
                {
                    new {
                        canonicalName = "Loan.LastModified",
                        order = "Descending"
                    }
                }
            };
            return requestBody;
        }

    }
}
