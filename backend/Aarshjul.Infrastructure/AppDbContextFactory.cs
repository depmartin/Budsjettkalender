using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aarshjul.Infrastructure;

/// <summary>
/// Design-time-factory slik at `dotnet ef migrations add ...` kan opprette konteksten uten å
/// kjøre web-startup eller Entra-konfigurasjon. Tilkoblingsstrengen brukes ikke ved
/// `migrations add` (kun ved `database update` mot en faktisk database).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AARSHJUL_DB")
            ?? "Server=(localdb)\\mssqllocaldb;Database=Aarshjul;Trusted_Connection=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
