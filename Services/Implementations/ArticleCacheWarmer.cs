using DaNangSafeMap.Services.Interfaces;

namespace DaNangSafeMap.Services.Implementations
{
    /// <summary>
    /// Pre-warms the in-memory cache so the first visitor to <c>/Article</c>
    /// doesn't pay the cold DB round-trip cost.
    ///
    /// Runs once on startup on a background thread. Safe to fail —
    /// if warmup throws (e.g. DB not ready yet), the page still works,
    /// just without the speedup on first hit.
    /// </summary>
    public class ArticleCacheWarmer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ArticleCacheWarmer> _log;

        public ArticleCacheWarmer(IServiceScopeFactory scopeFactory, ILogger<ArticleCacheWarmer> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;
        }

        public Task StartAsync(CancellationToken ct)
        {
            // Fire-and-forget so startup isn't blocked.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IArticleService>();

                    var cats = await svc.GetCategoriesAsync();
                    await svc.GetFeaturedArticlesAsync(4, null);
                    await svc.GetLatestArticlesAsync(12, null);
                    await svc.GetMostViewedArticlesAsync(10);

                    foreach (var c in cats)
                    {
                        if (string.IsNullOrEmpty(c.Slug)) continue;
                        await svc.GetLatestArticlesAsync(7, c.Slug);
                        await svc.GetLatestArticlesAsync(24, c.Slug);
                        await svc.GetFeaturedArticlesAsync(4, c.Slug);
                    }

                    _log.LogInformation("ArticleCacheWarmer: article caches warmed.");
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "ArticleCacheWarmer failed (non-fatal).");
                }
            }, ct);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
