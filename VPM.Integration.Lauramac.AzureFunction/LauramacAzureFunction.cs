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
        private LoanRequest secondLienLoanRequest;
        private LoanDocumentRequest loanDoumentRequest;
        private LoanDocumentRequest secondLienLoanDoumentRequest;

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
            secondLienLoanRequest = new LoanRequest
            {
                Loans = new List<LauramacLoan>(),
                TransactionIdentifier = "",
                OverrideDuplicateLoans = "0"
            };
            secondLienLoanDoumentRequest = new LoanDocumentRequest
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
                _logger.LogInformation("Timer trigger function executed at: {Time}", DateTime.Now);
                string token = await GetEncompassAccessTokenAsync();

                if (string.IsNullOrWhiteSpace(token) || token.Contains("Error") || token.Contains("Exception"))
                {
                    _logger.LogError("Failed to retrieve Encompass access token: {Token}", token);
                    return;
                }

                await CallLoanPipelineApiAsync(token);

                string sellerName = Environment.GetEnvironmentVariable("SellerName");

                switch (sellerName)
                {
                    case "Canopy":
                        await ProcessLoanSetAsync(loanRequest, loanDoumentRequest, "Canopy");
                        break;

                    case "Clarifii":
                        await ProcessLoanSetAsync(loanRequest, loanDoumentRequest, "Clarifii First Lie");

                        if (secondLienLoanRequest?.Loans?.Count > 0)
                        {
                            await ProcessLoanSetAsync(secondLienLoanRequest, secondLienLoanDoumentRequest, "Clarifii Second Lie");
                        }
                        else
                        {
                            _logger.LogInformation("No second lien loans found to process.");
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown seller name: {Seller}", sellerName);
                        break;
                }

                _logger.LogInformation("Next timer schedule at: {Next}", myTimer.ScheduleStatus?.Next);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred.");
            }
        }

        private async Task ProcessLoanSetAsync(LoanRequest loanReq, LoanDocumentRequest docReq, string context)
        {
            if (loanReq?.Loans == null || loanReq.Loans.Count == 0)
            {
                _logger.LogInformation("No loans found to process for {Context}.", context);
                return;
            }

            ImportResponse response = await _lauramacService.SendLoanDataAsync(loanReq);
            _logger.LogInformation("{Context} Loan Data Response: {Response}", context, response);

            if (response.Status == "Success")
            {
                _logger.LogInformation("{Context} loan data sent successfully.", context);
                var documentResponse = await _lauramacService.SendLoanDocumentDataAsync(docReq);
                _logger.LogInformation("{Context} Document Response: {Response}", context, documentResponse);
            }
            else
            {
                _logger.LogError("{Context} failed to send loan data: {Status}", context, response.Status);
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
            string baseUrl = Environment.GetEnvironmentVariable("EncompassApiBaseURL");
            string pipelineUrl = Environment.GetEnvironmentVariable("EncompassLoanPipelineURL");
            string documentPackage = Environment.GetEnvironmentVariable("DocumentPackageName");
            string azureDocumentPath = Environment.GetEnvironmentVariable("AzureDocumentPath");
            string endpointTemplate = Environment.GetEnvironmentVariable("EncompassGetDocumentsURL");
            string canopyTransactionIdentifier = Environment.GetEnvironmentVariable("CanopyTransactionIdentifier");

            string requestUrl = $"{baseUrl?.TrimEnd('/')}{pipelineUrl}";
            string json = JsonConvert.SerializeObject(RequestBody());
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _loanDataService.GetLoanData(requestUrl, content, token);
            _logger.LogInformation("Loan Pipeline Response: {Response}", response);

            if (string.IsNullOrWhiteSpace(response) || response == "[]")
            {
                _logger.LogInformation("No loans match the given criteria.");
                return;
            }

            if (!IsValidJson(response, out JToken parsedJson))
            {
                _logger.LogError("Invalid JSON in Loan Pipeline Response: {Response}", response);
                return;
            }

            List<EncompassLoan> loans;
            try
            {
                loans = JsonConvert.DeserializeObject<List<EncompassLoan>>(response);
                _logger.LogInformation("Number of Loans: {Count}", loans?.Count ?? 0);
            }
            catch (JsonException ex)
            {
                _logger.LogError("Error deserializing loan pipeline response: {Message}", ex.Message);
                return;
            }

            if (loans == null || loans.Count == 0) return;

            string sellerName = loans.FirstOrDefault()?.Fields?.FieldsCXNAME_DDPROVIDER;
            string transactionId = canopyTransactionIdentifier?.Replace("{transactionId}", DateTime.Now.ToString("yyyy-MM"));
            SetTransactionIds(transactionId);

            foreach (var loan in loans)
            {
                string lienPosition = loan.Fields?.Fields420;
                LogLoanInfo(loan);

                var docsResponse = await _loanDataService.GetAllLoanDocuments(token, loan.LoanId);
                if (string.IsNullOrWhiteSpace(docsResponse) || docsResponse == "[]")
                {
                    _logger.LogInformation("No documents found for Loan ID: {LoanId}", loan.LoanId);
                    continue;
                }

                if (!IsValidJson(docsResponse, out JToken parsedDocJson))
                {
                    _logger.LogError("Invalid JSON in Loan Documents Response: {Response}", docsResponse);
                    continue;
                }

                List<Attachment> attachments;
                try
                {
                    attachments = JsonConvert.DeserializeObject<List<Attachment>>(docsResponse) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogError("Error deserializing attachments: {Message}", ex.Message);
                    continue;
                }

                foreach (var attachment in attachments)
                {
                    if (!IsValidAttachment(attachment, documentPackage)) continue;
                    //loan.LoanId = "66b6fc88-f675-4cdd-b78a-214453cde1e9";
                    //attachment.Id = "eb00e165-4ce6-4580-a39a-555067afdaca";
                    var docUrl = await _loanDataService.GetDocumentUrl(loan.LoanId, attachment.Id, token);
                    if (string.IsNullOrWhiteSpace(docUrl) || !Uri.IsWellFormedUriString(docUrl, UriKind.Absolute))
                    {
                        _logger.LogError("Invalid or failed document URL for loan {LoanId}", loan.LoanId);
                        continue;
                    }

                    var downloaded = await _loanDataService.DownloadDocument(loan.LoanId, loan.Fields.Field4002, docUrl);
                    if (!downloaded) continue;

                    var lauramacLoan = MapToLauramacLoan(loan);
                    var loanDoc = BuildLoanDocument(loan.LoanId, loan.Fields.Field4002, azureDocumentPath);

                    AssignLoanByLienPosition(sellerName, lienPosition, lauramacLoan, loanDoc);
                    break;
                }
            }

            SetSellerNames(sellerName);
        }

        private bool IsValidJson(string input, out JToken parsed)
        {
            parsed = null;
            try
            {
                parsed = JToken.Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LogLoanInfo(EncompassLoan loan)
        {
            _logger.LogInformation("Loan ID: {LoanId}, Number: {LoanNumber}, Amount: {Amount}",
                loan.LoanId, loan.Fields?.LoanNumber, loan.Fields?.LoanAmount);
        }

        private bool IsValidAttachment(Attachment attachment, string expectedPackage)
        {
            return attachment.Type == "Image" &&
                   attachment.AssignedTo?.EntityName == expectedPackage &&
                   attachment.FileSize > 0;
        }

        private void SetTransactionIds(string transactionId)
        {
            loanRequest.TransactionIdentifier = transactionId;
            loanDoumentRequest.TransactionIdentifier = transactionId;
        }

        private Models.Lauramac.Request.Loan MapToLauramacLoan(EncompassLoan loan)
        {
            var f = loan.Fields;
            return new Models.Lauramac.Request.Loan
            {
                LoanID = loan.LoanId,
                LoanNumber = f.LoanNumber,
                LoanAmount = f.LoanAmount,
                NoteRate = f.Field3,
                LoanTerm = f.Field325,
                Purpose = f.Field19,
                Fico = f.CreditScore,
                OriginalLTV = f.LTV,
                OriginalCLTV = f.Field976,
                AppraisedValue = f.Field356,
                PurchasePrice = f.FieldsCXPURCHASEPRICE,
                DocType = f.DocType,
                AmortizationType = f.Field608,
                PropType = f.Field1401,
                Occupancy = f.OccupancyStatus,
                BorrowerFirstName = f.Field4000,
                BorrowerLastName = f.Field4002,
                BorrowerSSN = f.Field65,
                Address = f.Address1,
                City = f.City,
                State = f.State,
                Zip = f.Field15
            };
        }

        private LoanDocument BuildLoanDocument(string loanId, string borrowerLastName, string path)
        {
            return new LoanDocument
            {
                LoanID = loanId,
                Filename = $"{path}{loanId}_{borrowerLastName}_shippingfiles.pdf",
                isExternalDocument = false
            };
        }

        private void AssignLoanByLienPosition(string seller, string lien, Models.Lauramac.Request.Loan loan, LoanDocument doc)
        {
            if (seller == "Clarifii")
            {
                if (lien == "First Lie")
                {
                    loanRequest.Loans.Add(loan);
                    loanDoumentRequest.LoanDocuments.Add(doc);
                }
                else if (lien == "Second Lie")
                {
                    secondLienLoanRequest.Loans.Add(loan);
                    secondLienLoanDoumentRequest.LoanDocuments.Add(doc);
                }
            }
            else
            {
                loanRequest.Loans.Add(loan);
                loanDoumentRequest.LoanDocuments.Add(doc);
            }
        }

        private void SetSellerNames(string seller)
        {
            loanRequest.SellerName = seller;
            loanDoumentRequest.SellerName = seller;
            if (seller == "Clarifii")
            {
                secondLienLoanRequest.SellerName = seller;
                secondLienLoanDoumentRequest.SellerName = seller;
            }
        }



        private static Object RequestBody()
        {
            var sellerName = Environment.GetEnvironmentVariable("SellerName");
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
                    value = "04/11/2025", //DateTime.UtcNow.ToString("MM/dd/yyyy"),
                    matchType = "Equals",
                    precision = "Day"
                },
                new FilterTerm {
                    canonicalName = "Fields.CX.NAME_DDPROVIDER",
                    value = sellerName,
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
                    "Fields.675", "Fields.QM.X23", "Fields.QM.X25", "Fields.2278", "Fields.65","Fields.CX.PURCHASEPRICE","Fields.1550","Fields.356","Fields.CX.NAME_DDPROVIDER","Fields.420"
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
