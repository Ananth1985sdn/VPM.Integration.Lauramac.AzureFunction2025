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
        private readonly string _tokenUrl = "https://api.elliemae.com/oauth2/v1/token";
        private readonly string _pipelineUrl = "https://api.elliemae.com/encompass/v3/loanPipeline";

        private void SetEnvironmentVariables(Dictionary<string, string> variables)
        {
            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }
        }

        private static async Task<string> ReadTestDataAsync(string filePath)
        {
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }

        private static Object RequestBody()
        {
            var filterTerms = new List<FilterTerm>
        {
            new FilterTerm { canonicalName = "Loan.CurrentMilestoneName", value = new[] { "Started" }, matchType = "MultiValue", include = true },
            new FilterTerm { canonicalName = "Loan.LoanNumber", value = "5", matchType = "startsWith", include = false },
            new FilterTerm { canonicalName = "Fields.CX.DUEDILIGENCE_START_DT", value = "04/11/2025", matchType = "Equals", precision = "Day" },
            new FilterTerm { canonicalName = "Fields.CX.NAME_DDPROVIDER", value = "Canopy", matchType = "Exact", include = true }
        };

            return new
            {
                fields = new[]
                {
                "Loan.LoanNumber", "Fields.19", "Fields.608", "Loan.LoanAmount", "Loan.LTV", "Fields.976",
                "Loan.Address1", "Loan.City", "Loan.State", "Fields.15", "Fields.1041", "Loan.OccupancyStatus",
                "Fields.1401", "Fields.CX.VP.DOC.TYPE", "Fields.4000", "Fields.4002", "Fields.CX.CREDITSCORE",
                "Fields.325", "Fields.3", "Fields.742", "Fields.CX.VP.BUSINESS.PURPOSE", "Fields.1550",
                "Fields.675", "Fields.QM.X23", "Fields.QM.X25", "Fields.2278", "Fields.65", "Fields.CX.PURCHASEPRICE",
                "Fields.1550", "Fields.356", "Fields.CX.NAME_DDPROVIDER"
            },
                filter = new { @operator = "and", terms = filterTerms },
                orgType = "internal",
                loanOwnership = "AllLoans",
                sortOrder = new[] { new { canonicalName = "Loan.LastModified", order = "Descending" } }
            };
        }

        [Fact]
        public async Task TestCase_GetAccessTokenWithValidCred()
        {
            SetEnvironmentVariables(new Dictionary<string, string>
            {
                ["EncompassUsername"] = "gananth@encompass:TEBE11212117",
                ["EncompassPassword"] = "Welcome@123",
                ["EncompassClientId"] = "2n4b1uv",
                ["EncompassClientSecret"] = "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h"
            });

            var userName = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");

            var mockJson = @"{ ""access_token"": ""0004R2RHhOxb7g64wHTgFwmNbOgv"", ""token_type"": ""Bearer"" }";
            var mockLoanDataService = new Mock<ILoanDataService>();

            mockLoanDataService.Setup(service => service.GetToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), _tokenUrl))
                .ReturnsAsync(mockJson);

            var loanService = BuildService(mockLoanDataService.Object);

            var tokenJson = await loanService.GetToken(userName, password, clientId, clientSecret, _tokenUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.NotNull(tokenObj.access_token);
        }

        [Fact]
        public async Task TestCase_InvalidUsernameOrPassword()
        {
            SetEnvironmentVariables(new Dictionary<string, string>
            {
                ["EncompassUsername"] = "gananth@encompass:TEBE11212117",
                ["EncompassPassword"] = "WrongPassword",
                ["EncompassClientId"] = "2n4b1uv",
                ["EncompassClientSecret"] = "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h"
            });

            var userName = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");

            var mockJson = @"{ ""error_description"": ""Invalid username or password."", ""error"": ""invalid_grant"" }";
            var mockLoanDataService = new Mock<ILoanDataService>();

            mockLoanDataService.Setup(service => service.GetToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockJson);

            var loanService = BuildService(mockLoanDataService.Object);

            var tokenJson = await loanService.GetToken(userName, password, clientId, clientSecret, _tokenUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.Null(tokenObj.access_token);
        }

        [Fact]
        public async Task TestCase_Get_Valid_LoanData()
        {
            SetEnvironmentVariables(new Dictionary<string, string>
            {
                ["EncompassAuthToken"] = "0004R2RHhOxb7g64wHTgFwmNbOgv"
            });
            var authToken = Environment.GetEnvironmentVariable("EncompassAuthToken");       

            var expectedContent = await ReadTestDataAsync(@"TestData/SuccessLoanData.json");

            var mockLoanDataService = new Mock<ILoanDataService>();
            mockLoanDataService.Setup(service => service.GetLoanData(
                It.IsAny<string>(), It.IsAny<StringContent>(), It.IsAny<string>()))
                .ReturnsAsync(expectedContent);

            var loanService = mockLoanDataService.Object;
            var requestBodyJson = JsonConvert.SerializeObject(RequestBody());
            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

            var result = await loanService.GetLoanData(_pipelineUrl, content, authToken);
            var actualLoans = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(result);
            var expectedLoans = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(expectedContent);

            Assert.Equal(expectedLoans[0].LoanId, actualLoans[0].LoanId);
        }

        [Fact]
        public async Task TestCase_Get_Empty_LoanData()
        {
            SetEnvironmentVariables(new Dictionary<string, string>
            {
                ["EncompassAuthToken"] = "0004R2RHhOxb7g64wHTgFwmNbOgv"
            });
            var authToken = Environment.GetEnvironmentVariable("EncompassAuthToken");
            var expectedContent = await ReadTestDataAsync(@"TestData/FailureLoanData.json");

            var mockLoanDataService = new Mock<ILoanDataService>();
            mockLoanDataService.Setup(service => service.GetLoanData(
                It.IsAny<string>(), It.IsAny<StringContent>(), It.IsAny<string>()))
                .ReturnsAsync(expectedContent);

            var loanService = mockLoanDataService.Object;
            var requestBodyJson = JsonConvert.SerializeObject(RequestBody());
            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

            var result = await loanService.GetLoanData(_pipelineUrl, content, authToken);
            var loans = JsonConvert.DeserializeObject<List<Models.Encompass.Response.Loan>>(result);

            Assert.Empty(loans);
        }

        [Fact]
        public async Task TestCase_GetAccessTokenForLauraMac()
        {
            SetEnvironmentVariables(new Dictionary<string, string>
            {
                ["LauraMacUsername"] = "admin",
                ["LauraMacPassword"] = "LauraMac@123",
                ["LauraMacApiBaseURL"] = "https://app.uat.lauramac.io/apis",
                ["LauraMacTokenURL"] = "/client/authorization"
            });

            var userName = Environment.GetEnvironmentVariable("LauraMacUsername");
            var password = Environment.GetEnvironmentVariable("LauraMacPassword");


            var tokenRequestUrl = $"{Environment.GetEnvironmentVariable("LauraMacApiBaseURL")}{Environment.GetEnvironmentVariable("LauraMacTokenURL")}";
            var mockJson = @"{ ""access_token"": ""0004R2RHhOxb7g64wHTgFwmNbOgv"", ""token_type"": ""Bearer"" }";

            var mockLauramacService = new Mock<ILauramacService>();
            mockLauramacService.Setup(service => service.GetLauramacAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockJson);

            var lauramacService = BuildService(mockLauramacService.Object);

            var tokenJson = await lauramacService.GetLauramacAccessToken(userName, password, tokenRequestUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            Assert.NotNull(tokenObj.access_token);
        }

        [Fact]
        public async Task TestCase_SendLoanDataAsyncToLauraMac()
        {
            var mockJson = await ReadTestDataAsync(@"TestData/LauramacImportLoanSuccessResponse.json");
            var expectedResponse = JsonConvert.DeserializeObject<ImportResponse>(mockJson);

            var mockLauramacService = new Mock<ILauramacService>();
            mockLauramacService.Setup(service => service.SendLoanDataAsync(
                It.IsAny<LoanRequest>())).ReturnsAsync(expectedResponse);

            var lauramacService = BuildService(mockLauramacService.Object);

            var requestBodyJson = await ReadTestDataAsync(@"TestData/LauramacImportLoansRequest.json");
            var request = JsonConvert.DeserializeObject<LoanRequest>(requestBodyJson);

            var actualResponse = await lauramacService.SendLoanDataAsync(request);

            Assert.Equal(expectedResponse.Status, actualResponse.Status);

        }

        private static T BuildService<T>(T implementation) where T : class
        {
            var services = new ServiceCollection();
            services.AddSingleton(implementation);
            return services.BuildServiceProvider().GetService<T>();
        }
    }

}
