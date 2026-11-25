using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LMERP.Infrastructure.Identity;

public static class IdentitySeeder
{
    private static readonly string[] Roles = ["Administrator", "LoanOfficer", "CollectionsManager", "Analyst"];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in Roles)
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<int>(role));

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        var users = services.GetRequiredService<UserManager<LoanUser>>();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var environment = services.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment() && !(await users.GetUsersInRoleAsync("Administrator")).Any())
            {
                var logger = services.GetRequiredService<ILogger<IdentitySeeder>>();
                logger.LogWarning("No Administrator user exists. Configure BootstrapAdmin:Email and BootstrapAdmin:Password using a secret store to create one.");
            }
            return;
        }
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new LoanUser { Email = email, UserName = email, FullName = "Workspace Administrator", EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create the configured bootstrap administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
        if (!await users.IsInRoleAsync(user, "Administrator")) await users.AddToRoleAsync(user, "Administrator");
    }
}
