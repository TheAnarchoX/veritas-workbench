using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Veritas.Infrastructure.Persistence;
using Veritas.Infrastructure.Policies;

namespace Veritas.Tests;

public sealed class RobotsPolicyServiceTests
{
    [Fact]
    public async Task Robots_cache_lookup_works_with_sqlite_datetimeoffset_values()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "veritas-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        try
        {
            var dbOptions = new DbContextOptionsBuilder<VeritasDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var db = new VeritasDbContext(dbOptions);
            await db.Database.EnsureCreatedAsync();

            var handler = new StaticRobotsHandler("User-agent: *\nAllow: /\nDisallow: /private\n");
            var service = new RobotsPolicyService(
                new HttpClient(handler),
                db,
                Options.Create(new RobotsOptions { CacheHours = 1, UserAgent = "VeritasWorkbench/0.1" }));

            var uri = new Uri("https://example.test/media/1");
            var first = await service.CanFetchAsync(uri, "VeritasWorkbench/0.1", CancellationToken.None);
            var second = await service.CanFetchAsync(uri, "VeritasWorkbench/0.1", CancellationToken.None);

            Assert.True(first.Allowed);
            Assert.True(second.Allowed);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    private sealed class StaticRobotsHandler(string robotsText) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(robotsText)
            });
        }
    }
}
