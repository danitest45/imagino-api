using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
            var username = dto.Username;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = await GenerateUniqueUsernameAsync(dto.Email);
            }
            else
            {
                var existing = await _repository.GetByUsernameAsync(username);
                if (existing != null)
                    throw new ArgumentException("Username already in use");
            }

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Username = username!,
                PhoneNumber = dto.PhoneNumber,
                Subscription = dto.Subscription,
                Credits = dto.Credits
            };

            await _repository.CreateAsync(user);
            return user;
        }

        public Task<string> GenerateUsernameFromEmailAsync(string email) =>
            GenerateUniqueUsernameAsync(email);

        private async Task<string> GenerateUniqueUsernameAsync(string seed)
        {
            var prefix = seed.Split('@')[0];
            var baseName = Regex.Replace(prefix.ToLowerInvariant(), "[^a-z0-9]", "");
            if (string.IsNullOrEmpty(baseName))
                baseName = "user";

            var username = baseName;
            var rnd = new Random();
            while (await _repository.GetByUsernameAsync(username) != null)
            {
                username = $"{baseName}{rnd.Next(1000, 9999)}";
            }

            return username;
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

            if (!string.IsNullOrEmpty(dto.Username))
            {
                var existing = await _repository.GetByUsernameAsync(dto.Username);
                if (existing != null && existing.Id != user.Id)
                    throw new ArgumentException("Username already in use");
                user.Username = dto.Username;
            }

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber;

            if (dto.Subscription.HasValue)
                user.Subscription = dto.Subscription.Value;

            if (dto.Credits.HasValue)
                user.Credits = dto.Credits.Value;

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

