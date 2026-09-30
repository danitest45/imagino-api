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
            Assert.Equal(SubscriptionType.Free, created.Subscription);
            Assert.Equal(0, created.Credits);
            Assert.Null(created.Plan);
            Assert.Null(created.SubscriptionStatus);
            Assert.Null(created.StripeCustomerId);
            Assert.Null(created.StripeSubscriptionId);
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
        public async Task ProfileUpdate_PreservesEconomicFields()
        {
            var user = new User
            {
                Id = "user-a", Email = "a@example.test", Username = "old",
                Credits = 17, Subscription = SubscriptionType.Free,
                Plan = "PRO", SubscriptionStatus = "active",
                StripeCustomerId = "customer", StripeSubscriptionId = "subscription"
            };
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetByIdAsync("user-a")).ReturnsAsync(user);
            repo.Setup(r => r.GetByUsernameAsync("new")).ReturnsAsync((User?)null);
            var service = new UserService(repo.Object, new Mock<IStorageService>().Object);

            await service.UpdateAsync("user-a", new UserProfileUpdateDto { Username = "new" });

            Assert.Equal("new", user.Username);
            Assert.Equal(17, user.Credits);
            Assert.Equal(SubscriptionType.Free, user.Subscription);
            Assert.Equal("PRO", user.Plan);
            Assert.Equal("active", user.SubscriptionStatus);
            Assert.Equal("customer", user.StripeCustomerId);
            Assert.Equal("subscription", user.StripeSubscriptionId);
            repo.Verify(r => r.UpdateAsync(user), Times.Once);
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
