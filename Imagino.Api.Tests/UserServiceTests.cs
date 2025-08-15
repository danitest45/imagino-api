using System.Threading.Tasks;
using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services;
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

            var service = new UserService(repo.Object);
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

            var service = new UserService(repo.Object);
            var username = await service.GenerateUsernameFromEmailAsync("jane@example.com");

            Assert.StartsWith("jane", username);
            Assert.NotEqual("jane", username);
        }
    }
}
