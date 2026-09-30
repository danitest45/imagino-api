using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services.Storage;
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
        private readonly IStorageService _storage;

        public UserService(IUserRepository repository, IStorageService storage)
        {
            _repository = repository;
            _storage = storage;
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
                Subscription = SubscriptionType.Free,
                Credits = 0,
                Plan = null,
                SubscriptionStatus = null,
                StripeCustomerId = null,
                StripeSubscriptionId = null
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

        public async Task<User?> UpdateAsync(string id, UserProfileUpdateDto dto)
        {
            var user = await _repository.GetByIdAsync(id);
            if (user == null) return null;

            if (!string.IsNullOrEmpty(dto.Username))
            {
                var existing = await _repository.GetByUsernameAsync(dto.Username);
                if (existing != null && existing.Id != user.Id)
                    throw new ArgumentException("Username already in use");
                user.Username = dto.Username;
            }

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber;

            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateProfileAsync(id, user.Username, user.PhoneNumber, user.UpdatedAt);
            return user;
        }

        public async Task DeleteAsync(string id) =>
            await _repository.DeleteAsync(id);

        public async Task<string?> UpdateProfileImageAsync(string id, IFormFile file)
        {
            var user = await _repository.GetByIdAsync(id);
            if (user == null) return null;

            var fileName = $"profile-images/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            using var stream = file.OpenReadStream();
            var url = await _storage.UploadAsync(stream, fileName, file.ContentType);

            user.ProfileImageUrl = url;
            user.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateProfileImageAsync(id, url, user.UpdatedAt);

            return user.ProfileImageUrl;
        }

        public async Task<bool> IncrementCreditsAsync(string userId, int amount) =>
            await _repository.IncrementCreditsAsync(userId, amount);

        public async Task<int?> GetCreditsAsync(string userId) =>
            await _repository.GetCreditsAsync(userId);
    }
}

