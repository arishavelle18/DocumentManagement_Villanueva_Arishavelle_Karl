using DocumentManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DocumentManagement.Tests.Infrastructure;

/// <summary>
/// Boots the real API in memory, but swaps SQL Server for SQLite in-memory and
/// points file storage at a throwaway temp folder. One factory = one isolated
/// database + one isolated storage folder.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database lives only as long as its connection is open,
    // so we keep one connection open for the whole lifetime of the factory.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public string StorageRootPath { get; } =
        Path.Combine(Path.GetTempPath(), "DocumentManagement.Tests", Guid.NewGuid().ToString("N"));

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Absolute path → Path.Combine(ContentRootPath, RootPath) resolves to this folder,
        // so the real Storage/ folder is never touched.
        builder.UseSetting("FileStorage:RootPath", StorageRootPath);

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration. Since EF Core 9 the provider config is held in
            // IDbContextOptionsConfiguration<T>; leaving it would register two providers.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Build the schema from the EF model (the migrations are SQL Server specific).
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        _connection.Dispose();

        if (Directory.Exists(StorageRootPath))
            Directory.Delete(StorageRootPath, recursive: true);
    }
}
