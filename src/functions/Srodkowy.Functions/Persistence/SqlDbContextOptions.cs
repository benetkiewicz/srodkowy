using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Srodkowy.Functions.Persistence;

public static class SqlDbContextOptions
{
    private const string ApplicationName = "Srodkowy.Functions";
    private const int CompatibilityLevel = 170;

    public static void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        var normalizedConnectionString = NormalizeConnectionString(connectionString);

        if (IsAzureSql(normalizedConnectionString))
        {
            optionsBuilder.UseAzureSql(normalizedConnectionString, options => options.UseCompatibilityLevel(CompatibilityLevel));
            return;
        }

        optionsBuilder.UseSqlServer(normalizedConnectionString, options => options.UseCompatibilityLevel(CompatibilityLevel));
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (!builder.ContainsKey("Application Name") && !builder.ContainsKey("ApplicationName"))
        {
            builder["Application Name"] = ApplicationName;
        }

        return builder.ConnectionString;
    }

    private static bool IsAzureSql(string connectionString)
    {
        return connectionString.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Authentication=Active Directory", StringComparison.OrdinalIgnoreCase);
    }
}
