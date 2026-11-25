using LMERP.Application.Abstractions;
using LMERP.Application.Services;
using LMERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LoanDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("LoanDb")));
        services.AddScoped<ILoanManagementRepository, LoanManagementRepository>();
        services.AddScoped<IEmiRepaymentService, EmiRepaymentService>();
        services.AddHostedService<LateFeeBackgroundService>();
        return services;
    }
}
