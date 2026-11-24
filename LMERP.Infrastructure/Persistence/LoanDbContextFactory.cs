using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LMERP.Infrastructure.Persistence;

/// <summary>
/// Creates LoanDbContext for EF Core tools, including when Visual Studio selects
/// the Infrastructure project as the Package Manager Console startup project.
/// </summary>
public sealed class LoanDbContextFactory : IDesignTimeDbContextFactory<LoanDbContext>
{
    public LoanDbContext CreateDbContext(string[] args)
    {
        var serverProjectDirectory = FindServerProjectDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(serverProjectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<LoanDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("LoanDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:LoanDb' was not found in the server configuration.");
        }

        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LoanDbContext(options);
    }

    private static string FindServerProjectDirectory()
    {
        var startingDirectories = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var startingDirectory in startingDirectories)
        {
            var directory = new DirectoryInfo(startingDirectory);
            while (directory is not null)
            {
                var serverProject = Path.Combine(directory.FullName, "LMERP.Server");
                if (File.Exists(Path.Combine(serverProject, "appsettings.json")))
                {
                    return serverProject;
                }

                if (directory.Name.Equals("LMERP.Server", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate LMERP.Server/appsettings.json from the current directory or Infrastructure build output.");
    }
}
