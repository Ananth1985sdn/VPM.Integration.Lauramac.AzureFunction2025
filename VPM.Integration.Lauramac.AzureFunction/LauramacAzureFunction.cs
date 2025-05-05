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
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response;

namespace VPM.Integration.Lauramac.AzureFunction
{
    public class LauramacAzureFunction
    {
        private readonly ILogger _logger;
        private readonly ILoanDataService _loanDataService;
        private readonly ILauramacService _lauramacService;
        private LoanRequest loanRequest;
        private LoanDocumentRequest loanDoumentRequest;
        public LauramacAzureFunction(ILoggerFactory loggerFactory, ILoanDataService loanDataService, ILauramacService lauramacService)
        {
            _logger = loggerFactory.CreateLogger<LauramacAzureFunction>();
            _loanDataService = loanDataService;
            _lauramacService = lauramacService;
            loanRequest = new LoanRequest
            {
                Loans = new List<LauramacLoan>(),
                TransactionIdentifier = "",
                OverrideDuplicateLoans = "0"
            };
            loanDoumentRequest = new LoanDocumentRequest
            {
                LoanDocuments = new List<LoanDocument>(),
                TransactionIdentifier = "",
            };
        }

        [Function("LauramacAzureFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            try
            {
                _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
                string token = await GetEncompassAccessTokenAsync();

                if (myTimer.ScheduleStatus is not null)
                {
                    if (!string.IsNullOrEmpty(token) && !token.Contains("Error") && !token.Contains("Exception"))
                    {
                        await CallLoanPipelineApiAsync(token);
                        if (loanRequest.Loans.Count == 0)
                        {
                            _logger.LogInformation("No loans found to process.");
                            return;
                        }
                        ImportResponse response = await _lauramacService.SendLoanDataAsync(loanRequest);
                        _logger.LogInformation("Lauramac Response: {Response}", response);
                        if(response.Status == "Success")
                        {
                            _logger.LogInformation("Loan data sent successfully.");
                            var documentResponse = await _lauramacService.SendLoanDocumentDataAsync(loanDoumentRequest);
                            _logger.LogInformation("Lauramac Document Response: {Response}", documentResponse);
                        }
                        else
                        {
                            _logger.LogError("Failed to send loan data: {Message}", response.Status);
                        }
                        
                    }
                    else
                    {
                        _logger.LogError($"Failed to retrieve Encompass access token.{token}");
                    }
                    _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }

        public async Task<string> GetEncompassAccessTokenAsync()
        {
            var baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var tokenEndpoint = Environment.GetEnvironmentVariable("EncompassTokenUrl");

            var credentials = new EncompassCredentials();

            var fullUrl = $"{baseUrl?.TrimEnd('/')}{tokenEndpoint}";

            var token = await _loanDataService.GetToken(
                credentials.Username,
                credentials.Password,
                credentials.ClientId,
                credentials.ClientSecret,
                fullUrl
            );

            return token;
        }


        private async Task CallLoanPipelineApiAsync(string token)
        {
            var baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            var pipelineUrl = Environment.GetEnvironmentVariable("EncompassLoanPipelineURL");
            var documentPackage = Environment.GetEnvironmentVariable("DocumentPackageName");
            var azureDocumentPath = Environment.GetEnvironmentVariable("AzureDocumentPath");
            string sellerName = string.Empty;
            var requestUrl = $"{baseUrl?.TrimEnd('/')}{pipelineUrl}";
            var requestBody = RequestBody();
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _loanDataService.GetLoanData(requestUrl, content, token);
            _logger.LogInformation("Loan Pipeline Response: {Response}", response);

            if (string.IsNullOrWhiteSpace(response) || response == "[]")
            {
                _logger.LogInformation("No loans match the given criteria.");
                return;
            }
            else if (response.Contains("Error") || response.Contains("Exception"))
            {
                _logger.LogError("Error in Loan Pipeline API: {Response}", response);
                return;
            }
            else
            {
                try
                {
                    try
                    {
                        _ = JToken.Parse(response);
                    }
                    catch (JsonReaderException ex)
                    {
                        _logger.LogInformation(
                            "Invalid JSON in GetLoanData: {Response}. Error: {ErrorMessage}",
                            response, ex.Message);
                        return;
                    }

                    var loans = JsonConvert.DeserializeObject<List<EncompassLoan>>(response);
                    _logger.LogInformation("Number of Loans: {LoanCount}", loans?.Count ?? 0);

                    var endpointTemplate = Environment.GetEnvironmentVariable("EncompassGetDocumentsURL");
                    var canopyTransactionIdentifier = Environment.GetEnvironmentVariable("CanopyTransactionIdentifier");
                    DateTime currentDateTime = DateTime.Now;
                    string transactionId = currentDateTime.ToString("yyyy-MM");
                    var transactionIdentifier = canopyTransactionIdentifier.Replace("{transactionId}", transactionId);
                    loanRequest.TransactionIdentifier = transactionIdentifier;
                    loanDoumentRequest.TransactionIdentifier = transactionIdentifier;

                    foreach (var loan in loans ?? Enumerable.Empty<EncompassLoan>())
                    {
                        sellerName = string.IsNullOrEmpty(sellerName)
                            ? loan.Fields.FieldsCXNAME_DDPROVIDER
                            : sellerName;

                        _logger.LogInformation(
                            "Loan ID: {LoanId}, Loan Number: {LoanNumber}, Amount: {LoanAmount}",
                            loan.LoanId, loan.Fields.LoanNumber, loan.Fields.LoanAmount);

                        var documentsResponse = await _loanDataService.GetAllLoanDocuments(token, loan.LoanId);
                        if (string.IsNullOrWhiteSpace(documentsResponse) || documentsResponse == "[]")
                        {
                            _logger.LogInformation("No documents found for Loan ID: {LoanId}", loan.LoanId);
                            continue;
                        }
                        else if (documentsResponse.Contains("Error") || documentsResponse.Contains("Exception"))
                        {
                            _logger.LogError("Error in GetAllLoanDocuments: {Response}", documentsResponse);
                            continue;
                        }
                        else
                        {
                            try
                            {
                                _ = JToken.Parse(documentsResponse); // Validate JSON
                            }
                            catch (JsonReaderException ex)
                            {
                                _logger.LogInformation(
                                    "Invalid JSON in Loan Documents Response: {Response}. Error: {ErrorMessage}",
                                    documentsResponse, ex.Message);
                                continue;
                            }

                            _logger.LogInformation(
                                "Attachments for Loan {LoanNumber}: {Attachments}",
                                loan.Fields.LoanNumber, documentsResponse);

                            List<Attachment> attachments;
                            try
                            {
                                attachments = JsonConvert.DeserializeObject<List<Attachment>>(documentsResponse)
                                              ?? new List<Attachment>();
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError("Error deserializing attachments: {Message}", ex.Message);
                                continue;
                            }

                            foreach (var attachment in attachments)
                            {
                                if (attachment.AssignedTo?.EntityName != documentPackage ||
                                    attachment.FileSize <= 0 ||
                                    attachment.Type != "Image")
                                    continue;

                                _logger.LogInformation(
                                    "Attachment Title: {Title}, CreatedBy: {CreatedBy}, File Size: {Size}",
                                    attachment.Title, attachment.AssignedTo?.EntityName, attachment.FileSize);

                                // TEMP: Overriding for testing/demo
                                loan.LoanId = "66b6fc88-f675-4cdd-b78a-214453cde1e9";
                                attachment.Id = "eb00e165-4ce6-4580-a39a-555067afdaca";

                                var url = await _loanDataService.GetDocumentUrl(loan.LoanId, attachment.Id, token);
                                if (string.IsNullOrWhiteSpace(url) || url.Contains("Error") || url.Contains("Exception"))
                                {
                                    _logger.LogError("Error in GetDocumentUrl: {Response}", url);
                                    continue;
                                }

                                else if (string.IsNullOrWhiteSpace(url) && !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                                {
                                    _logger.LogInformation("Document URL: {Url}", url);
                                    continue;
                                }
                                else
                                {
                                    _logger.LogInformation("Document URL: {Url}", url);

                                    var documentDownloaded = await _loanDataService.DownloadDocument(
                                        loan.LoanId, loan.Fields.Field4002, url);

                                    if (!documentDownloaded) continue;

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
                                        BorrowerFirstName = loan.Fields.Field4000,
                                        BorrowerLastName = loan.Fields.Field4002,
                                        BorrowerSSN = loan.Fields.Field65,
                                        Address = loan.Fields.Address1,
                                        City = loan.Fields.City,
                                        State = loan.Fields.State,
                                        Zip = loan.Fields.Field15
                                    };

                                    loanRequest.Loans.Add(lauramacLoan);
                                    loanDoumentRequest.LoanDocuments.Add(new LoanDocument
                                    {
                                        LoanID = loan.LoanId,
                                        Filename = $"{azureDocumentPath}{loan.LoanId}_{loan.Fields.Field4002}_shippingfiles.pdf",//$"D:/local/temp/{loan.LoanId}_{loan.Fields.Field4002}_shippingfiles.pdf",
                                        isExternalDocument = false,
                                        ExternalDocumentLink = null,
                                        ExternalFileId = null,
                                        ExternalRequestId = null
                                    });
                                    break;
                                }
                            }
                        }
                    }

                    loanRequest.SellerName = sellerName;
                    loanDoumentRequest.SellerName = sellerName;
                }
                catch (JsonException ex)
                {
                    _logger.LogError("Error deserializing loan pipeline response: {Message}", ex.Message);
                }
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
