using Application.Abstractions;
using Application.Services;
using Domain.Interfaces;
using Infrastructure.Persistence.EF;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("Default")));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IStudentRepository, StudentRepository>();

        // External services
        services.AddSingleton<IClock, Clock>();

        // Caching
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
}
