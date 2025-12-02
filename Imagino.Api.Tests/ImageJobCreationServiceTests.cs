using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Imagino.Api.DTOs.Image;
using Imagino.Api.Errors;
using Imagino.Api.Models;
using Imagino.Api.Models.Image;
using Imagino.Api.Repository;
using Imagino.Api.Repositories.Image;
using Imagino.Api.Services.Image;
using Imagino.Api.Services.Image.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Bson;
using Xunit;

namespace Imagino.Api.Tests
{
    public class ImageJobCreationServiceTests
    {
        [Fact]
        public async Task CreateJobAsync_GoogleProvider_CompletesAndUpdatesJob()
        {
            var (service, dependencies) = BuildService(ImageProviderType.GoogleGemini, new ProviderJobResult("provider-job", ImageJobStatus.Completed, "http://image"));

            var request = new CreateImageJobRequest
            {
                ModelSlug = "model-slug",
                Params = JsonDocument.Parse("{\"prompt\":\"hello\"}")
            };

            var response = await service.CreateJobAsync(request, "user-1");

            Assert.NotNull(dependencies.InsertedJob);
            Assert.Equal(dependencies.InsertedJob!.Id, response.JobId);
            Assert.Equal(ImageJobStatus.Created, dependencies.InsertedJob.Status);
            Assert.Equal(ImageJobStatus.Completed.ToString(), response.Status);

            Assert.NotNull(dependencies.UpdatedJob);
            Assert.Equal("provider-job", dependencies.UpdatedJob!.ProviderJobId);
            Assert.Equal(ImageJobStatus.Completed, dependencies.UpdatedJob.Status);
            Assert.Contains("http://image", dependencies.UpdatedJob.ImageUrls);
            Assert.Equal(dependencies.InsertedJob.Id, dependencies.UpdatedJob.JobId);
        }

        [Fact]
        public async Task CreateJobAsync_ReplicateProvider_RemainsInProgressWithProviderJobId()
        {
            var (service, dependencies) = BuildService(ImageProviderType.Replicate, new ProviderJobResult("provider-job", ImageJobStatus.Queued, null));

            var request = new CreateImageJobRequest
            {
                ModelSlug = "model-slug",
                Params = JsonDocument.Parse("{\"prompt\":\"hello\"}")
            };

            var response = await service.CreateJobAsync(request, "user-1");

            Assert.Equal(ImageJobStatus.Queued.ToString(), response.Status);
            Assert.NotNull(dependencies.UpdatedJob);
            Assert.Equal("provider-job", dependencies.UpdatedJob!.ProviderJobId);
            Assert.Empty(dependencies.UpdatedJob.ImageUrls);
        }

        [Fact]
        public async Task CreateJobAsync_OnProviderFailure_MarksJobAsFailedAndRefundsCredits()
        {
            var providerException = new Exception("provider failed");
            var (service, dependencies) = BuildService(
                ImageProviderType.GoogleGemini,
                providerResult: null,
                providerException: providerException);

            var request = new CreateImageJobRequest
            {
                ModelSlug = "model-slug",
                Params = JsonDocument.Parse("{\"prompt\":\"hello\"}")
            };

            await Assert.ThrowsAsync<Exception>(() => service.CreateJobAsync(request, "user-1"));

            dependencies.UserRepository.Verify(r => r.IncrementCreditsAsync("user-1", 1), Times.Once);
            Assert.NotNull(dependencies.UpdatedJob);
            Assert.Equal(ImageJobStatus.Failed, dependencies.UpdatedJob!.Status);
            Assert.Equal(providerException.Message, dependencies.UpdatedJob.ErrorMessage);
        }

        private static (ImageJobCreationService Service, DependencySnapshot Dependencies) BuildService(
            ImageProviderType providerType,
            ProviderJobResult? providerResult,
            Exception? providerException = null)
        {
            var user = new User { Id = "user-1", Credits = 5 };
            var model = new ImageModel
            {
                Id = "model-1",
                ProviderId = "provider-1",
                Slug = "model-slug",
                Pricing = new ImageModelPricing { CreditsPerImage = 1 }
            };

            var version = new ImageModelVersion
            {
                VersionTag = "v1",
                Pricing = new ImageModelPricing { CreditsPerImage = 1 }
            };

            var resolvedParams = new BsonDocument { { "prompt", "hello" } };

            var modelResolver = new Mock<IModelResolverService>();
            modelResolver
                .Setup(m => m.ResolveModelAndVersionAsync("model-slug", null, It.IsAny<JsonDocument>()))
                .ReturnsAsync(new ResolvedModelVersion(model, version, resolvedParams));

            var providerRepository = new Mock<IImageModelProviderRepository>();
            providerRepository
                .Setup(p => p.GetByIdAsync(model.ProviderId))
                .ReturnsAsync(new ImageModelProvider { Id = model.ProviderId, Name = "provider", ProviderType = providerType });

            var userRepository = new Mock<IUserRepository>();
            userRepository.Setup(r => r.GetByIdAsync(user.Id!)).ReturnsAsync(user);
            userRepository.Setup(r => r.DecrementCreditsAsync(user.Id!, 1)).ReturnsAsync(true);
            userRepository.Setup(r => r.IncrementCreditsAsync(user.Id!, 1)).ReturnsAsync(true);

            var jobRepository = new Mock<IImageJobRepository>();
            var sequence = new MockSequence();
            ImageJob? insertedJob = null;
            ImageJob? updatedJob = null;

            jobRepository.InSequence(sequence)
                .Setup(r => r.InsertAsync(It.IsAny<ImageJob>()))
                .Callback<ImageJob>(j => insertedJob = j)
                .Returns(Task.CompletedTask);

            jobRepository.InSequence(sequence)
                .Setup(r => r.UpdateAsync(It.IsAny<ImageJob>()))
                .Callback<ImageJob>(j => updatedJob = j)
                .Returns(Task.CompletedTask);

            var providerClient = new Mock<IImageProviderClient>();
            providerClient.SetupGet(p => p.ProviderType).Returns(providerType);

            if (providerException != null)
            {
                providerClient
                    .Setup(p => p.CreateJobAsync(It.IsAny<ImageModelProvider>(), version, resolvedParams))
                    .ThrowsAsync(providerException);
            }
            else
            {
                providerClient
                    .Setup(p => p.CreateJobAsync(It.IsAny<ImageModelProvider>(), version, resolvedParams))
                    .ReturnsAsync(providerResult!);
            }

            var logger = new Mock<ILogger<ImageJobCreationService>>();

            var service = new ImageJobCreationService(
                modelResolver.Object,
                jobRepository.Object,
                userRepository.Object,
                providerRepository.Object,
                new[] { providerClient.Object },
                logger.Object);

            var dependencies = new DependencySnapshot
            {
                InsertedJob = insertedJob,
                UpdatedJob = updatedJob,
                JobRepository = jobRepository,
                UserRepository = userRepository
            };

            return (service, dependencies);
        }

        private class DependencySnapshot
        {
            public ImageJob? InsertedJob { get; set; }
            public ImageJob? UpdatedJob { get; set; }
            public Mock<IImageJobRepository> JobRepository { get; set; } = default!;
            public Mock<IUserRepository> UserRepository { get; set; } = default!;
        }
    }
}
