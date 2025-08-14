using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Imagino.Api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;

        public UserService(IUserRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<User>> GetAllAsync() =>
            await _repository.GetAllAsync();

        public async Task<User?> GetByIdAsync(string id) =>
            await _repository.GetByIdAsync(id);

        public async Task<User> CreateAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            await _repository.CreateAsync(user);
            return user;
        }

        public async Task<User?> UpdateAsync(string id, UpdateUserDto dto)
        {
            var user = await _repository.GetByIdAsync(id);
            if (user == null) return null;

            if (!string.IsNullOrEmpty(dto.Email))
                user.Email = dto.Email;

            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            if (!string.IsNullOrEmpty(dto.ProfileImageUrl))
                user.ProfileImageUrl = dto.ProfileImageUrl;

            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);
            return user;
        }

        public async Task DeleteAsync(string id) =>
            await _repository.DeleteAsync(id);

        public async Task<string?> UpdateProfileImageAsync(string id, IFormFile file)
        {
            var user = await _repository.GetByIdAsync(id);
            if (user == null) return null;

            var uploads = Path.Combine("wwwroot", "profile-images");
            Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            user.ProfileImageUrl = $"/profile-images/{fileName}";
            user.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(user);

            return user.ProfileImageUrl;
        }
    }
}

