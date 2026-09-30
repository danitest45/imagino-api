using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.Services.Storage;
using Moq;
using Microsoft.AspNetCore.Http;
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
                Id = "user-a", Email = "a@example.test", PasswordHash = "original-hash",
                ProfileImageUrl = "https://example.test/original.png", Username = "old",
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
            Assert.Equal("a@example.test", user.Email);
            Assert.Equal("original-hash", user.PasswordHash);
            Assert.Equal("https://example.test/original.png", user.ProfileImageUrl);
            Assert.Equal(17, user.Credits);
            Assert.Equal(SubscriptionType.Free, user.Subscription);
            Assert.Equal("PRO", user.Plan);
            Assert.Equal("active", user.SubscriptionStatus);
            Assert.Equal("customer", user.StripeCustomerId);
            Assert.Equal("subscription", user.StripeSubscriptionId);
            repo.Verify(r => r.UpdateProfileAsync("user-a", "new", user.PhoneNumber, It.IsAny<System.DateTime>()), Times.Once);
            repo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task AvatarUpload_UpdatesOnlyProfileImageField()
        {
            var user = new User { Id = "user-a", Username = "a", Credits = 17 };
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetByIdAsync("user-a")).ReturnsAsync(user);
            var storage = new Mock<IStorageService>();
            storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://example.test/avatar.png");
            var service = new UserService(repo.Object, storage.Object);
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var file = new FormFile(stream, 0, stream.Length, "File", "avatar.png")
            {
                Headers = new HeaderDictionary(), ContentType = "image/png"
            };

            var url = await service.UpdateProfileImageAsync("user-a", file);

            Assert.Equal("https://example.test/avatar.png", url);
            Assert.Equal(17, user.Credits);
            repo.Verify(r => r.UpdateProfileImageAsync("user-a", url, It.IsAny<System.DateTime>()), Times.Once);
            repo.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
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
