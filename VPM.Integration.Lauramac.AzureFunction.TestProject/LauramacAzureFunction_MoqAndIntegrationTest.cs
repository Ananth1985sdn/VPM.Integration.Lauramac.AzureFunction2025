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
        [Fact]
        public async Task TestGetLoanData_MockVsIntegration_ShouldMatch()
        {
            // Arrange: environment setup
            Environment.SetEnvironmentVariable("EncompassPipelineUrl", "https://api.elliemae.com/encompass/v3/loanPipeline");
            Environment.SetEnvironmentVariable("EncompassAuthToken", "0004R2RHhOxb7g64wHTgFwmNbOgv");

            Environment.SetEnvironmentVariable("EncompassUsername", "gananth@encompass:TEBE11212117");
            Environment.SetEnvironmentVariable("EncompassPassword", "Welcome@123");
            Environment.SetEnvironmentVariable("EncompassClientId", "2n4b1uv");
            Environment.SetEnvironmentVariable("EncompassClientSecret", "*GjzokQW3Gw9bQtVQ#B5n$EHyi5yHW&jVIbTS0Ql7M7C38CHDcccm4icw56uAa0h");

            var userName = Environment.GetEnvironmentVariable("EncompassUsername");
            var password = Environment.GetEnvironmentVariable("EncompassPassword");
            var clientId = Environment.GetEnvironmentVariable("EncompassClientId");
            var clientSecret = Environment.GetEnvironmentVariable("EncompassClientSecret");


            var encompassPipelineUrl = Environment.GetEnvironmentVariable("EncompassPipelineUrl");
            var encompassAuthToken = Environment.GetEnvironmentVariable("EncompassAuthToken");
            var tokenUrl = "https://api.elliemae.com/oauth2/v1/token";
            var requestBody = RequestBody();
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 🔹 Integration: use real HttpClient + service
            var loggerMock = new Mock<ILogger<LoanDataService>>();
            var httpClient = new HttpClient(); // NOTE: this will hit the real API
            var realService = new LoanDataService(loggerMock.Object, httpClient);

            var accessToken = await realService.GetToken(userName, password, clientId, clientSecret, tokenUrl);

            var realResult = await realService.GetLoanData(encompassPipelineUrl, content, accessToken);
            var realLoanResponse = JsonConvert.DeserializeObject<List<Loan>>(realResult);

            // 🔹 Mock: return expected JSON from file
            var expectedContent = await File.ReadAllTextAsync(@"TestData/SuccessLoanData.json", Encoding.UTF8);
            var loanDataServiceMock = new Mock<ILoanDataService>();

            loanDataServiceMock
                .Setup(s => s.GetLoanData(
                    It.IsAny<string>(),
                    It.IsAny<StringContent>(),
                    It.IsAny<string>()))
                .ReturnsAsync(expectedContent);

            var mockService = loanDataServiceMock.Object;
            var mockResult = await mockService.GetLoanData(encompassPipelineUrl, content, encompassAuthToken);
            var mockLoanResponse = JsonConvert.DeserializeObject<List<Loan>>(mockResult);

            // Assert: compare mock vs real
            Assert.Equal(mockLoanResponse.Count, realLoanResponse.Count);
            Assert.Equal(mockLoanResponse[0].LoanId, realLoanResponse[0].LoanId);
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
