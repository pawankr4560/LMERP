using LMERP.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LMERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILoanManagementService, LoanManagementService>();
        return services;
    }
}
