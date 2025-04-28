using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using System.Text;
using VPM.Integration.Lauramac.AzureFunction.Interface;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Request;
using VPM.Integration.Lauramac.AzureFunction.Models.Encompass.Response;
using VPM.Integration.Lauramac.AzureFunction.Services;

namespace VPM.Integration.Lauramac.AzureFunction.Tests
{
    public class LauramacAzureFunction_MoqAndIntegrationTest
    {
        private const string EncompassPipelineUrl = "https://api.elliemae.com/encompass/v3/loanPipeline";
        private const string EncompassTokenUrl = "https://api.elliemae.com/oauth2/v1/token";
        private const string TestAccessToken = "0004R2RHhOxb7g64wHTgFwmNbOgv";

        private static readonly Dictionary<string, string> EncompassCredentials = new()
        {
            { "EncompassUsername", "gananth@encompass:TEBE11212117" },
            { "EncompassPassword", "Welcome@123" },
            { "EncompassClientId", "2n4b1uv" },
            { "EncompassClientSecret", "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h" }
        };

        [Fact]
        public async Task TestGetLoanData_MockVsIntegration_ShouldMatch()
        {
            SetEnvironmentVariables();

            var userName = GetEnvironmentVariable("EncompassUsername");
            var password = GetEnvironmentVariable("EncompassPassword");
            var clientId = GetEnvironmentVariable("EncompassClientId");
            var clientSecret = GetEnvironmentVariable("EncompassClientSecret");
            var authToken = GetEnvironmentVariable("EncompassAuthToken") ?? TestAccessToken;

            var requestBody = RequestBody();
            var content = CreateJsonContent(requestBody);

            var realService = CreateRealLoanDataService();

            var accessToken = await realService.GetToken(userName, password, clientId, clientSecret, EncompassTokenUrl);
            var realResult = await realService.GetLoanData(EncompassPipelineUrl, content, accessToken);
            var realLoanResponse = JsonConvert.DeserializeObject<List<Loan>>(realResult);

            var expectedMockContent = await File.ReadAllTextAsync(@"TestData/SuccessLoanData.json", Encoding.UTF8);

            var mockLoanDataService = new Mock<ILoanDataService>();
            mockLoanDataService
                .Setup(s => s.GetLoanData(It.IsAny<string>(), It.IsAny<StringContent>(), It.IsAny<string>()))
                .ReturnsAsync(expectedMockContent);

            var mockResult = await mockLoanDataService.Object.GetLoanData(EncompassPipelineUrl, content, authToken);
            var mockLoanResponse = JsonConvert.DeserializeObject<List<Loan>>(mockResult);

            Assert.Equal(mockLoanResponse.Count, realLoanResponse.Count);
            Assert.Equal(mockLoanResponse[0].LoanId, realLoanResponse[0].LoanId);
        }

        private static LoanDataService CreateRealLoanDataService()
        {
            var loggerMock = new Mock<ILogger<LoanDataService>>();
            var httpClient = new HttpClient();
            return new LoanDataService(loggerMock.Object, httpClient);
        }

        private static StringContent CreateJsonContent(object requestBody)
        {
            var json = JsonConvert.SerializeObject(requestBody);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static void SetEnvironmentVariables()
        {
            foreach (var (key, value) in EncompassCredentials)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
            Environment.SetEnvironmentVariable("EncompassPipelineUrl", EncompassPipelineUrl);
            Environment.SetEnvironmentVariable("EncompassAuthToken", TestAccessToken);
        }

        private static string GetEnvironmentVariable(string key) => Environment.GetEnvironmentVariable(key) ?? string.Empty;

        private static object RequestBody()
        {
            var filterTerms = new List<FilterTerm>
        {
            new FilterTerm
            {
                canonicalName = "Loan.CurrentMilestoneName",
                value = new[] { "Started" },
                matchType = "MultiValue",
                include = true
            },
            new FilterTerm
            {
                canonicalName = "Loan.LoanNumber",
                value = "5",
                matchType = "startsWith",
                include = false
            },
            new FilterTerm
            {
                canonicalName = "Fields.CX.DUEDILIGENCE_START_DT",
                value = "04/11/2025",
                matchType = "Equals",
                precision = "Day"
            },
            new FilterTerm
            {
                canonicalName = "Fields.CX.NAME_DDPROVIDER",
                value = "Canopy",
                matchType = "Exact",
                include = true
            }
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
                filter = new
                {
                    @operator = "and",
                    terms = filterTerms
                },
                orgType = "internal",
                loanOwnership = "AllLoans",
                sortOrder = new[]
                {
                new
                {
                    canonicalName = "Loan.LastModified",
                    order = "Descending"
                }
            }
            };
        }
    }

}
