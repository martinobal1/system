using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IsSuas.Infrastructure.Persistence;

public sealed class IsSuasDbContextFactory : IDesignTimeDbContextFactory<IsSuasDbContext>
{
    public IsSuasDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IsSuasDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=issuas;Username=issuas;Password=issuas_dev")
            .Options;
        return new IsSuasDbContext(options);
    }
}
