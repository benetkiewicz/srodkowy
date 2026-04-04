using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Srodkowy.Functions.Persistence;

public sealed class SrodkowyDbContextFactory : IDesignTimeDbContextFactory<SrodkowyDbContext>
{
    public SrodkowyDbContext CreateDbContext(string[] args)
    {
        var settingsDirectory = FindSettingsDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(settingsDirectory)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
            ?? configuration["Values:Database:ConnectionString"]
            ?? throw new InvalidOperationException("Missing configuration value 'Database:ConnectionString'.");

        var optionsBuilder = new DbContextOptionsBuilder<SrodkowyDbContext>();
        SqlDbContextOptions.Configure(optionsBuilder, connectionString);

        return new SrodkowyDbContext(optionsBuilder.Options);
    }

    private static string FindSettingsDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "local.settings.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
