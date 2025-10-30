using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Imagino.Api.DTOs.Image
{
    public class CreateImageJobRequest
    {
        public string? PresetId { get; set; }

        [Required]
        public string ModelSlug { get; set; } = default!;

        public JsonDocument Params { get; set; } = JsonDocument.Parse("{}");
    }
}
