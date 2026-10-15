using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ApiFactory : WebApplicationFactory<Program>
{
    private const string TestDbPrefix = "EkomasTest_";
    private readonly string _connectionString;

    public ApiFactory()
    {
        // Server credentials only. The database name is made unique below.
        var server = Environment.GetEnvironmentVariable("EKOMAS_TEST_SQL")
            ?? "Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True";

        _connectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"{TestDbPrefix}{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");   // not "Development": keeps your user-secrets out of tests
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-123456");
        builder.UseSetting("Jwt:Issuer", "ekomas");
        builder.UseSetting("Jwt:Audience", "ekomas-clients");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return host;
    }

    private bool _dbDropped;

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_dbDropped)
        {
            _dbDropped = true;   // set first, so the re-entrant call skips this block

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var name = db.Database.GetDbConnection().Database;
            if (name.StartsWith(TestDbPrefix))
                db.Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}
