using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imagino.Api.Controllers;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services.ImageGeneration;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Imagino.Api.Tests;

public class JobsControllerTests
{
    [Fact]
    public async Task GetLatestJobs_ReturnsIdFieldForEachJob()
    {
        // Arrange
        var jobs = new List<ImageJob>
        {
            new()
            {
                Id = "1",
                JobId = "job1",
                ImageUrls = new List<string>{"url1"},
                Prompt = "prompt1",
                AspectRatio = "1:1",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = "2",
                JobId = "job2",
                ImageUrls = new List<string>{"url2"},
                Prompt = "prompt2",
                AspectRatio = "1:1",
                CreatedAt = DateTime.UtcNow
            }
        };

        var jobRepo = new Mock<IImageJobRepository>();
        jobRepo.Setup(r => r.GetLatestAsync(12)).ReturnsAsync(jobs);

        var userRepo = new Mock<IUserRepository>();
        var jobsService = new Mock<IJobsService>();

        var controller = new JobsController(jobsService.Object, jobRepo.Object, userRepo.Object);

        // Act
        var result = await controller.GetLatestJobs();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);

        foreach (var item in response)
        {
            var idProp = item.GetType().GetProperty("Id");
            Assert.NotNull(idProp);
            var value = idProp!.GetValue(item) as string;
            Assert.False(string.IsNullOrEmpty(value));
        }
    }
}

