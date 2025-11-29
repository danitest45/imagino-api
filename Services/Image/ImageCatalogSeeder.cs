using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imagino.Api.Models.Image;
using Imagino.Api.Repositories.Image;
using MongoDB.Bson;

namespace Imagino.Api.Services.Image
{
    public class ImageCatalogSeeder
    {
        private readonly IImageModelProviderRepository _providerRepository;
        private readonly IImageModelRepository _modelRepository;
        private readonly IImageModelVersionRepository _versionRepository;
        private readonly IImageModelPresetRepository _presetRepository;

        public ImageCatalogSeeder(
            IImageModelProviderRepository providerRepository,
            IImageModelRepository modelRepository,
            IImageModelVersionRepository versionRepository,
            IImageModelPresetRepository presetRepository)
        {
            _providerRepository = providerRepository;
            _modelRepository = modelRepository;
            _versionRepository = versionRepository;
            _presetRepository = presetRepository;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var existingProvider = await _providerRepository.GetByNameAsync("replicate");
            if (existingProvider != null)
            {
                return;
            }

            var provider = new ImageModelProvider
            {
                Name = "replicate",
                Status = "Active",
                ProviderType = ImageProviderType.Replicate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _providerRepository.InsertAsync(provider);

            provider = await _providerRepository.GetByNameAsync("replicate") ?? provider;

            var fluxModel = new ImageModel
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Slug = "flux",
                DisplayName = "Flux",
                ProviderId = provider.Id!,
                Capabilities = new ImageModelCapabilities
                {
                    Image = true,
                    Inpaint = false,
                    Upscale = false
                },
                Visibility = ImageModelVisibility.Public,
                Status = ImageModelStatus.Active,
                Pricing = new ImageModelPricing
                {
                    CreditsPerImage = 5,
                    MinCredits = 0
                },
                Tags = new List<string> { "photo", "flux" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _modelRepository.InsertAsync(fluxModel);

            var fluxVersion = new ImageModelVersion
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ModelId = fluxModel.Id!,
                VersionTag = "1.1-pro",
                EndpointUrl = "https://api.replicate.com/v1/models/black-forest-labs/flux-1.1-pro/predictions",
                ParamSchema = BsonDocument.Parse(@"{
  'type': 'object',
  'properties': {
    'prompt': { 'type': 'string' },
    'steps': { 'type': 'integer', 'minimum': 1, 'maximum': 50 },
    'guidance': { 'type': 'number', 'minimum': 0, 'maximum': 20 },
    'width': { 'type': 'integer' },
    'height': { 'type': 'integer' },
    'aspect_ratio': { 'type': 'string' }
  },
  'required': ['prompt']
}".Replace("'", "\"")),
                Defaults = BsonDocument.Parse(@"{
  'steps': 30,
  'guidance': 3.5,
  'width': 1024,
  'height': 1024,
  'aspect_ratio': '1:1'
}".Replace("'", "\"")),
                Pricing = new ImageModelPricing
                {
                    CreditsPerImage = 5,
                    MinCredits = 0
                },
                Status = ImageModelVersionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _versionRepository.InsertAsync(fluxVersion);
            await _modelRepository.SetDefaultVersionAsync(fluxModel.Id!, fluxVersion.Id!);

            var fluxPreset = new ImageModelPreset
            {
                ModelId = fluxModel.Id!,
                ModelVersionId = fluxVersion.Id!,
                Slug = "ultra-clean",
                Name = "Ultra Clean",
                Description = "Preset otimizado para imagens limpas",
                Params = BsonDocument.Parse(@"{
  'guidance': 4.0,
  'steps': 40,
  'aspect_ratio': '16:9'
}".Replace("'", "\"")),
                Locks = new List<string> { "steps" },
                Visibility = ImageModelVisibility.Public,
                Status = ImageModelPresetStatus.Active,
                Ordering = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _presetRepository.InsertAsync(fluxPreset);

            var seedreamModel = new ImageModel
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Slug = "seedream-4",
                DisplayName = "Seedream 4",
                ProviderId = provider.Id!,
                Capabilities = new ImageModelCapabilities
                {
                    Image = true,
                    Inpaint = false,
                    Upscale = false
                },
                Visibility = ImageModelVisibility.Premium,
                Status = ImageModelStatus.Active,
                Pricing = new ImageModelPricing
                {
                    CreditsPerImage = 8,
                    MinCredits = 0
                },
                Tags = new List<string> { "seedream", "premium" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _modelRepository.InsertAsync(seedreamModel);

            var seedreamVersion = new ImageModelVersion
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ModelId = seedreamModel.Id!,
                VersionTag = "2025-10-01",
                EndpointUrl = "https://api.replicate.com/v1/models/bytedance/seedream-4/predictions",
                ParamSchema = BsonDocument.Parse(@"{
  'type': 'object',
  'properties': {
    'prompt': { 'type': 'string' },
    'size': { 'type': 'string', 'enum': ['1K', '2K', '4K'] },
    'width': { 'type': 'integer', 'minimum': 512, 'maximum': 4096 },
    'height': { 'type': 'integer', 'minimum': 512, 'maximum': 4096 },
    'max_images': { 'type': 'integer', 'minimum': 1, 'maximum': 4 },
    'aspect_ratio': { 'type': 'string', 'enum': ['1:1', '4:3', '16:9', '9:16'] },
    'enhance_prompt': { 'type': 'boolean' },
    'sequential_image_generation': { 'type': 'string', 'enum': ['disabled', 'enabled'] }
  },
  'required': ['prompt']
}".Replace("'", "\"")),
                Defaults = BsonDocument.Parse(@"{
  'size': '2K',
  'width': 2048,
  'height': 2048,
  'max_images': 1,
  'aspect_ratio': '4:3',
  'enhance_prompt': true,
  'sequential_image_generation': 'disabled'
}".Replace("'", "\"")),
                Pricing = new ImageModelPricing
                {
                    CreditsPerImage = 8,
                    MinCredits = 0
                },
                Status = ImageModelVersionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _versionRepository.InsertAsync(seedreamVersion);
            await _modelRepository.SetDefaultVersionAsync(seedreamModel.Id!, seedreamVersion.Id!);

            var seedreamPreset = new ImageModelPreset
            {
                ModelId = seedreamModel.Id!,
                ModelVersionId = seedreamVersion.Id!,
                Slug = "2k-sharp",
                Name = "2K Sharp",
                Description = "Configuração com foco em nitidez 2K",
                Params = BsonDocument.Parse(@"{
  'size': '2K',
  'width': 2048,
  'height': 1152,
  'enhance_prompt': true,
  'max_images': 2,
  'aspect_ratio': '16:9'
}".Replace("'", "\"")),
                Locks = new List<string> { "size" },
                Visibility = ImageModelVisibility.Premium,
                Status = ImageModelPresetStatus.Active,
                Ordering = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _presetRepository.InsertAsync(seedreamPreset);
        }
    }
}
