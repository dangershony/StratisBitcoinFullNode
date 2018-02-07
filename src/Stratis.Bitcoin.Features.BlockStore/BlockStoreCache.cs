using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreCache
    {
        Task<PowBlock> GetBlockAsync(uint256 blockid);

        void AddToCache(PowBlock powBlock);

        /// <summary>
        /// Determine if a block already exists in the cache.
        /// </summary>
        /// <param name="blockid">Block id.</param>
        /// <returns><c>true</c> if the block hash can be found in the cache, otherwise return <c>false</c>.</returns>
        bool Exist(uint256 blockid);
    }

    public class BlockStoreCache : IBlockStoreCache
    {
        private readonly IBlockRepository blockRepository;

        private readonly MemoryCache<uint256, PowBlock> cache;

        public BlockStoreCachePerformanceCounter PerformanceCounter { get; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The maximum amount of blocks the cache can contain.</summary>
        public readonly int MaxCacheBlocksCount;

        public BlockStoreCache(
            IBlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings)
        {
            Guard.NotNull(blockRepository, nameof(blockRepository));

            // Initialize 'MaxCacheBlocksCount' with default value of maximum 300 blocks or with user defined value.
            // Value of 300 is chosen because it covers most of the cases when not synced node is connected and trying to sync from us.
            this.MaxCacheBlocksCount = nodeSettings.ConfigReader.GetOrDefault("maxCacheBlocksCount", 300);

            this.cache = new MemoryCache<uint256, PowBlock>(this.MaxCacheBlocksCount);

            this.blockRepository = blockRepository;
            this.dateTimeProvider = dateTimeProvider;
            this.PerformanceCounter = this.BlockStoreCachePerformanceCounterFactory();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public virtual BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new BlockStoreCachePerformanceCounter(this.dateTimeProvider);
        }

        public async Task<PowBlock> GetBlockAsync(uint256 blockid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockid), blockid);
            Guard.NotNull(blockid, nameof(blockid));

            PowBlock powBlock;
            if (this.cache.TryGetValue(blockid, out powBlock))
            {
                this.PerformanceCounter.AddCacheHitCount(1);
                this.logger.LogTrace("(-)[CACHE_HIT]:'{0}'", powBlock);
                return powBlock;
            }

            this.PerformanceCounter.AddCacheMissCount(1);

            powBlock = await this.blockRepository.GetAsync(blockid);
            if (powBlock != null)
            {
                this.cache.AddOrUpdate(blockid, powBlock);
                this.PerformanceCounter.AddCacheSetCount(1);
            }

            this.logger.LogTrace("(-)[CACHE_MISS]:'{0}'", powBlock);
            return powBlock;
        }

        public void AddToCache(PowBlock powBlock)
        {
            uint256 blockid = powBlock.GetHash();
            this.logger.LogTrace("({0}:'{1}')", nameof(powBlock), blockid);

            this.cache.AddOrUpdate(blockid, powBlock);
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public bool Exist(uint256 blockid)
        {
            return this.cache.TryGetValue(blockid, out PowBlock unused);
        }
    }
}
