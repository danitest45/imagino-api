using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Imagino.Api.Security;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        [HttpGet]
        public ActionResult Get()
        {
            var userId =
               User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
               User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found");

            return Ok(userId);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetById(string id)
        {
            if (!IsSelf(id)) return NotFound();
            var user = await _service.GetByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(ToDto(user));
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> GetMe()
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();
            var user = await _service.GetByIdAsync(userId);
            return user == null ? NotFound() : Ok(ToDto(user));
        }

        [HttpPost]
        [Authorize(Policy = AdminAuthorization.Policy)]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                var user = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UserProfileUpdateDto dto)
        {
            if (!IsSelf(id)) return NotFound();
            var user = await _service.UpdateAsync(id, dto);
            if (user == null) return NotFound();
            return Ok(ToDto(user));
        }

        [HttpPut("me")]
        public async Task<ActionResult<UserDto>> UpdateMe([FromBody] UserProfileUpdateDto dto)
        {
            var userId = CurrentUserId();
            return userId == null ? Unauthorized() : await Update(userId, dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!IsSelf(id)) return NotFound();
            await _service.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/profile-image")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadProfileImage(string id, [FromForm] UploadProfileImageDto form)
        {
            if (!IsSelf(id)) return NotFound();
            var file = form.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File not provided" });

            var imageUrl = await _service.UpdateProfileImageAsync(id, file);
            if (imageUrl == null) return NotFound();

            return Ok(new { imageUrl });
        }

        [HttpPost("me/profile-image")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadMyProfileImage([FromForm] UploadProfileImageDto form)
        {
            var userId = CurrentUserId();
            return userId == null ? Unauthorized() : await UploadProfileImage(userId, form);
        }

        [HttpPost("{id}/credits")]
        [Authorize(Policy = AdminAuthorization.Policy)]
        public async Task<IActionResult> AddCredits(string id, [FromBody] UpdateCreditsDto dto)
        {
            if (dto.Amount <= 0) return BadRequest(new { message = "Amount must be positive" });
            var success = await _service.IncrementCreditsAsync(id, dto.Amount);
            if (!success) return NotFound();
            var credits = await _service.GetCreditsAsync(id);
            return Ok(new { credits });
        }

        [HttpGet("credits")]
        public async Task<ActionResult> GetCredits()
        {
            var userId =
               User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
               User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found");

            var credits = await _service.GetCreditsAsync(userId);
            if (credits is null) return NotFound();
            return Ok(new { credits });
        }

        private static UserDto ToDto(User user) =>
            new(user.Id!, user.Email, user.GoogleId, user.ProfileImageUrl, user.Username, user.PhoneNumber, user.Subscription, user.Credits, user.CreatedAt, user.UpdatedAt);

        private string? CurrentUserId() =>
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        private bool IsSelf(string id) =>
            string.Equals(CurrentUserId(), id, StringComparison.Ordinal);
    }
}

