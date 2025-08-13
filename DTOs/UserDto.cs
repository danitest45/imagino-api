using System;

namespace Imagino.Api.DTOs
{
    public record UserDto(
        string Id,
        string? Email,
        string? GoogleId,
        string? ProfileImageUrl,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}

