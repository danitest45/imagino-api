using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Imagino.Api.Models.Image;

namespace Imagino.Api.DTOs.Image
{
    public class ImageModelProviderAuthDto : IValidatableObject
    {
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelProviderAuthMode Mode { get; set; }

        public string? SecretRef { get; set; }

        public string? EncBlob { get; set; }

        public string? EncKeyId { get; set; }

        public string? Header { get; set; } = "Authorization";

        public string? Scheme { get; set; }

        public string? BaseUrl { get; set; }

        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Mode == ImageModelProviderAuthMode.SecretRef && string.IsNullOrWhiteSpace(SecretRef))
            {
                yield return new ValidationResult("SecretRef is required when Mode is SecretRef", new[] { nameof(SecretRef) });
            }

            if (Mode == ImageModelProviderAuthMode.Encrypted)
            {
                if (string.IsNullOrWhiteSpace(EncBlob))
                {
                    yield return new ValidationResult("EncBlob is required when Mode is Encrypted", new[] { nameof(EncBlob) });
                }

                if (string.IsNullOrWhiteSpace(EncKeyId))
                {
                    yield return new ValidationResult("EncKeyId is required when Mode is Encrypted", new[] { nameof(EncKeyId) });
                }
            }
        }
    }

    public class CreateImageModelProviderDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelProviderStatus Status { get; set; } = ImageModelProviderStatus.Active;

        [Required]
        public ImageModelProviderAuthDto Auth { get; set; } = new();
    }

    public class UpdateImageModelProviderDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageModelProviderStatus Status { get; set; }

        [Required]
        public ImageModelProviderAuthDto Auth { get; set; } = new();
    }

    public class ImageModelProviderDto : CreateImageModelProviderDto
    {
        public string? Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
