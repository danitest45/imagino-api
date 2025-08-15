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
            var user = await _service.GetByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(ToDto(user));
        }

        [HttpPost]
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
        public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UpdateUserDto dto)
        {
            var user = await _service.UpdateAsync(id, dto);
            if (user == null) return NotFound();
            return Ok(ToDto(user));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/profile-image")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadProfileImage(string id, [FromForm] UploadProfileImageDto form)
        {
            var file = form.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File not provided" });

            var imageUrl = await _service.UpdateProfileImageAsync(id, file);
            if (imageUrl == null) return NotFound();

            return Ok(new { imageUrl });
        }

        private static UserDto ToDto(User user) =>
            new(user.Id!, user.Email, user.GoogleId, user.ProfileImageUrl, user.Username, user.PhoneNumber, user.Subscription, user.Credits, user.CreatedAt, user.UpdatedAt);
    }
}

