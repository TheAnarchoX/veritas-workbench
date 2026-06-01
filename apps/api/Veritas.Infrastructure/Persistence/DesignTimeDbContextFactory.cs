using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veritas.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VeritasDbContext>
{
    public VeritasDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VeritasDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=veritas;Username=veritas;Password=veritas")
            .Options;

        return new VeritasDbContext(options);
    }
}
