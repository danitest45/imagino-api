using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Imagino.Api.Tests
{
    public class ReplicateJobsServiceTests
    {
        [Fact]
        public async Task GenerateImageAsync_ReturnsError_WhenInsufficientCredits()
        {
            // Arrange
            var httpClient = new HttpClient(new FakeHandler());
            var replicateSettings = Options.Create(new ReplicateSettings { ApiKey = "k", ModelUrl = "http://localhost", WebhookUrl = "http://localhost" });
            var imageSettings = Options.Create(new ImageGeneratorSettings { ImageCost = 5 });

            var jobRepo = new Mock<IImageJobRepository>();
            var userRepo = new Mock<IUserRepository>();
            userRepo.Setup(r => r.GetByIdAsync("user1")).ReturnsAsync(new User { Id = "user1", Credits = 1 });

            var service = new ReplicateJobsService(httpClient, replicateSettings, jobRepo.Object, userRepo.Object, imageSettings);
            var request = new ImageGenerationReplicateRequest { Prompt = "test" };

            // Act
            var result = await service.GenerateImageAsync(request, "user1");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Insufficient credits.", result.Errors);
        }

        private class FakeHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"1\",\"status\":\"succeeded\"}")
                };
                return Task.FromResult(response);
            }
        }
    }
}
