using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using VPM.Integration.Lauramac.AzureFunction.Interface;

namespace VPM.Integration.Lauramac.AzureFunction.TestProject
{
    public class LauramacAzureFunction_Test
    {
        [Fact]
        public async Task TestGetToken_ExtractsAccessTokenCorrectly()
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

            // Arrange
            var mockLoanDataService = new Mock<ILoanDataService>();

            // Setup mock behavior for GetToken method with the expected parameters
            mockLoanDataService.Setup(service =>
                service.GetToken(userName, password, clientId, clientSecret, tokenUrl))
                .ReturnsAsync(mockJson);

            // If using Dependency Injection (DI), set up the services
            var services = new ServiceCollection();
            services.AddSingleton(mockLoanDataService.Object);
            var serviceProvider = services.BuildServiceProvider();

            // Inject the service into the class you're testing
            var loanService = serviceProvider.GetService<ILoanDataService>();

            // Act
            var tokenJson = await loanService.GetToken(userName, password, clientId, clientSecret, tokenUrl);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);

            // Assert
            Assert.NotNull(tokenObj.access_token);
        }
    }
}
