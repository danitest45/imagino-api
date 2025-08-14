using Imagino.Api.DTOs;
using Imagino.Api.Models;
using Imagino.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<ActionResult<IEnumerable<UserDto>>> Get()
        {
            var users = await _service.GetAllAsync();
            return Ok(users.Select(ToDto));
        }

        [AllowAnonymous]
        [HttpGet("exists/{username}")]
        public async Task<ActionResult<bool>> UsernameExists(string username)
        {
            var user = await _service.GetByUsernameAsync(username);
            return Ok(user != null);
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
            var user = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
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
            new(
                user.Id!,
                user.Email,
                user.Username,
                user.PhoneNumber,
                user.Subscription,
                user.Credits,
                user.GoogleId,
                user.ProfileImageUrl,
                user.CreatedAt,
                user.UpdatedAt);
    }
}

