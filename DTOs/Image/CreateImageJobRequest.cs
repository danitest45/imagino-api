using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Imagino.Api.DTOs.Image
{
    public class CreateImageJobRequest
    {
        public string? PresetId { get; set; }

        public string? Model { get; set; }

        public string? Version { get; set; }

        [Required]
        public JsonDocument? Params { get; set; }
    }
}
