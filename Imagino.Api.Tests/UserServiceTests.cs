using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.Services.Storage;
using Moq;
using Xunit;

#nullable enable

namespace Imagino.Api.Tests
{
    public class UserServiceTests
    {
        [Fact]
        public async Task CreateAsync_GeneratesUniqueUsername_WhenUsernameMissing()
        {
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
                .Returns<string>(u => Task.FromResult<User?>(u == "john" ? new User() : null));
            User? created = null;
            repo.Setup(r => r.CreateAsync(It.IsAny<User>()))
                .Callback<User>(u => created = u)
                .Returns(Task.CompletedTask);

            var storage = new Mock<IStorageService>();
            var service = new UserService(repo.Object, storage.Object);
            var dto = new CreateUserDto { Email = "john@example.com", Password = "pass" };
            var user = await service.CreateAsync(dto);

            Assert.NotNull(created);
            Assert.StartsWith("john", created!.Username);
            Assert.NotEqual("john", created.Username);
        }

        [Fact]
        public async Task GenerateUsernameFromEmailAsync_GeneratesUnique_WhenDuplicateExists()
        {
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
                .Returns<string>(u => Task.FromResult<User?>(u == "jane" ? new User() : null));

            var storage = new Mock<IStorageService>();
            var service = new UserService(repo.Object, storage.Object);
            var username = await service.GenerateUsernameFromEmailAsync("jane@example.com");

            Assert.StartsWith("jane", username);
            Assert.NotEqual("jane", username);
        }

        [Fact]
        public async Task IncrementCreditsAsync_CallsRepository()
        {
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.IncrementCreditsAsync("1", 5)).ReturnsAsync(true);
            var storage = new Mock<IStorageService>();
            var service = new UserService(repo.Object, storage.Object);

            var result = await service.IncrementCreditsAsync("1", 5);

            Assert.True(result);
            repo.Verify(r => r.IncrementCreditsAsync("1", 5), Times.Once);
        }

        [Fact]
        public async Task GetCreditsAsync_ReturnsValue()
        {
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetCreditsAsync("1")).ReturnsAsync(10);
            var storage = new Mock<IStorageService>();
            var service = new UserService(repo.Object, storage.Object);

            var credits = await service.GetCreditsAsync("1");

            Assert.Equal(10, credits);
        }
    }
}
