using System;
using Microsoft.Extensions.Caching.Memory;

namespace Imagino.Api.Services.Image
{
    public interface IPublicImageModelCacheService
    {
        void BumpVersion();
        int GetVersion();
    }

    public class PublicImageModelCacheService : IPublicImageModelCacheService
    {
        private const string VersionKey = "public-image-models-cache-version";
        private readonly IMemoryCache _cache;

        public PublicImageModelCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void BumpVersion()
        {
            var version = GetVersion();
            _cache.Set(VersionKey, version + 1, TimeSpan.FromHours(1));
        }

        public int GetVersion()
        {
            return _cache.GetOrCreate(VersionKey, _ => 0);
        }
    }
}
