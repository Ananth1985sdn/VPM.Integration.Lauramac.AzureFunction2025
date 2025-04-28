using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using System.Text;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Request;
using VPM.Integration.Lauramac.AzureFunction.Models.Lauramac.Response;

namespace VPM.Integration.Lauramac.AzureFunction.TestProject
{
    public class LauramacAzureFunction_MoqTest
    {
        [Fact]
        public async Task TestCase_GetAccessTokenWithValidCred()
        {
            Environment.SetEnvironmentVariable("EncompassUsername", "gananth@encompass:TEBE11212117");
            Environment.SetEnvironmentVariable("EncompassPassword", "Welcome@123");
            Environment.SetEnvironmentVariable("EncompassClientId", "2n4b1uv");
            Environment.SetEnvironmentVariable("EncompassClientSecret", "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h");

            var userName = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");
            var tokenUrl = "https://api.elliemae.com/oauth2/v1/token";

            string mockJson = @"{
                                ""access_token"": ""0004R2RHhOxb7g64wHTgFwmNbOgv"",
                                ""token_type"": ""Bearer""
                                }";

            var mockLoanDataService = new Mock<ILoanDataService>();

            mockLoanDataService.Setup(service =>
                service.GetToken(userName, password, clientId, clientSecret, tokenUrl))
                .ReturnsAsync(mockJson);

            var services = new ServiceCollection();
            services.AddSingleton(mockLoanDataService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var loanService = serviceProvider.GetService<ILoanDataService>();

            var tokenJson = await loanService.GetToken(userName, password, clientId, clientSecret, tokenUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.NotNull(tokenObj.access_token);
        }

        [Fact]
        public async Task TestCase_InvalidUsernameOrPassword()
        {
            Environment.SetEnvironmentVariable("EncompassUsername", "gananth@encompass:TEBE11212117");
            Environment.SetEnvironmentVariable("EncompassPassword", "Welcome@1234");
            Environment.SetEnvironmentVariable("EncompassClientId", "2n4b1uv");
            Environment.SetEnvironmentVariable("EncompassClientSecret", "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h");

            var userName = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");
            var tokenUrl = "https://api.elliemae.com/oauth2/v1/token";

            string mockJson = @"{
                                ""error_description"": ""Invalid username or password. Please try again."",
                                ""error"": ""invalid_grant""
                                }";

            var mockLoanDataService = new Mock<ILoanDataService>();

            mockLoanDataService.Setup(service =>
                service.GetToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockJson);

            var services = new ServiceCollection();
            services.AddSingleton(mockLoanDataService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var loanService = serviceProvider.GetService<ILoanDataService>();

            var tokenJson = await loanService.GetToken(userName, password, clientId, clientSecret, tokenUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.Null(tokenObj.access_token);
        }

        [Fact]
        public async Task TestCase_Get_Valid_LoanData()
        {
            Environment.SetEnvironmentVariable("EncompassPipelineUrl", "https://api.elliemae.com/encompass/v3/loanPipeline");
            Environment.SetEnvironmentVariable("EncompassAuthToken", "0004R2RHhOxb7g64wHTgFwmNbOgv");

            var encompassPipelineUrl = Environment.GetEnvironmentVariable("EncompassPipelineUrl");
            var encompassAuthToken = Environment.GetEnvironmentVariable("EncompassAuthToken");

            var expectedContent = await File.ReadAllTextAsync(@"TestData/SuccessLoanData.json", Encoding.UTF8);

            var loanDataServiceMock = new Mock<ILoanDataService>();

            loanDataServiceMock
                .Setup(service => service.GetLoanData(
                    It.IsAny<string>(),
                    It.IsAny<StringContent>(),
                    It.IsAny<string>()))
                .ReturnsAsync(expectedContent);

            var service = loanDataServiceMock.Object;

            var requestBody = RequestBody();
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var result = await service.GetLoanData(encompassPipelineUrl, content, encompassAuthToken);
            var loanResponse = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(result);
            var staticLoanResponse = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(expectedContent);
            
            Assert.Equal(staticLoanResponse[0].LoanId, loanResponse[0].LoanId);
        }
        
        [Fact]
        public async Task TestCase_Get_Empty_LoanData()
        {
            Environment.SetEnvironmentVariable("EncompassPipelineUrl", "https://api.elliemae.com/encompass/v3/loanPipeline");
            Environment.SetEnvironmentVariable("EncompassAuthToken", "0004R2RHhOxb7g64wHTgFwmNbOgv");

            var encompassPipelineUrl = Environment.GetEnvironmentVariable("EncompassPipelineUrl");
            var encompassAuthToken = Environment.GetEnvironmentVariable("EncompassAuthToken");

            var expectedContent = await File.ReadAllTextAsync(@"TestData/FailureLoanData.json", Encoding.UTF8);

            var loanDataServiceMock = new Mock<ILoanDataService>();

            loanDataServiceMock
                .Setup(service => service.GetLoanData(
                    It.IsAny<string>(),
                    It.IsAny<StringContent>(),
                    It.IsAny<string>()))
                .ReturnsAsync(expectedContent);

            var service = loanDataServiceMock.Object;

            var requestBody = RequestBody();
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var result = await service.GetLoanData(encompassPipelineUrl, content, encompassAuthToken);
            var loanResponse = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(result);

            Assert.Equal(loanResponse.Count,0);
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

        [Fact]
        public async Task TestCase_GetAccessTokenForLauraMac()
        {
            Environment.SetEnvironmentVariable("LauraMacUsername", "admin");
            Environment.SetEnvironmentVariable("LauraMacPassword", "LauraMac@123");
            Environment.SetEnvironmentVariable("LauraMacApiBaseURL", "https://app.uat.lauramac.io/apis");
            Environment.SetEnvironmentVariable("LauraMacTokenURL", "/client/authorization");

            var userName = Environment.GetEnvironmentVariable("LauraMacUsername");
            var password = Environment.GetEnvironmentVariable("LauraMacPassword"); 
            var baseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
            var tokenUrl = Environment.GetEnvironmentVariable("LauraMacTokenURL");
            var tokenRequestUrl = $"{baseUrl}{tokenUrl}";

            string mockJson = @"{
                                ""access_token"": ""0004R2RHhOxb7g64wHTgFwmNbOgv"",
                                ""token_type"": ""Bearer""
                                }";

            var mockLauramacService = new Mock<ILauramacService>();

            mockLauramacService.Setup(service =>
                service.GetLauramacAccessToken(userName, password, tokenRequestUrl))
                .ReturnsAsync(mockJson);

            var services = new ServiceCollection();
            services.AddSingleton(mockLauramacService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var lauramacService = serviceProvider.GetService<ILauramacService>();

            var tokenJson = await lauramacService.GetLauramacAccessToken(userName, password, tokenRequestUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.NotNull(tokenObj.access_token);
        }
        [Fact]
        public async Task TestCase_SendLoanDataAsyncToLauraMac()
        {
            Environment.SetEnvironmentVariable("LauraMacUsername", "admin");
            Environment.SetEnvironmentVariable("LauraMacPassword", "LauraMac@123");
            Environment.SetEnvironmentVariable("LauraMacApiBaseURL", "https://app.uat.lauramac.io/apis");
            Environment.SetEnvironmentVariable("LauraMacTokenURL", "/client/authorization");

            var baseUrl = Environment.GetEnvironmentVariable("LauraMacApiBaseURL");
            var importLoansUrl = Environment.GetEnvironmentVariable("LauraMacImportLoansUrl");
            var importLoansRequestUrl = $"{baseUrl}{importLoansUrl}";

            string lauramacImportLoansResponseMockJson = await File.ReadAllTextAsync(@"TestData/LauramacImportLoanSuccessResponse.json", Encoding.UTF8); ;
            var lauramacImportLoansResponseMock = JsonConvert.DeserializeObject<ImportResponse>(lauramacImportLoansResponseMockJson);

            var mockLauramacService = new Mock<ILauramacService>();

            var lauramacImportLoansSerializedRequest = await File.ReadAllTextAsync(@"TestData/LauramacImportLoansRequest.json", Encoding.UTF8);
            var lauramacImportLoansRequest = JsonConvert.DeserializeObject<LoanRequest>(lauramacImportLoansSerializedRequest);

            mockLauramacService.Setup(service =>
                service.SendLoanDataAsync(It.IsAny<LoanRequest>()))
                .ReturnsAsync(lauramacImportLoansResponseMockJson);

            var services = new ServiceCollection();
            services.AddSingleton(mockLauramacService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var lauramacService = serviceProvider.GetService<ILauramacService>();

            var lauramacImportLoansResponseJson = await lauramacService.SendLoanDataAsync(lauramacImportLoansRequest);
            var lauramacImportLoansResponse = JsonConvert.DeserializeObject<ImportResponse>(lauramacImportLoansSerializedRequest);

            Assert.Equal(lauramacImportLoansResponse.Loans[0].LoanID, lauramacImportLoansResponseMock.Loans[0].LoanID);
        }
    }
}
