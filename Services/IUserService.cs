using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imagino.Api.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(string id);
        Task<User?> GetByUsernameAsync(string username);
        Task<User> CreateAsync(CreateUserDto dto);
        Task<User?> UpdateAsync(string id, UpdateUserDto dto);
        Task DeleteAsync(string id);
        Task<string?> UpdateProfileImageAsync(string id, IFormFile file);
    }
}

