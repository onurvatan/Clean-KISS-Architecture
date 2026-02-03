using Application.Abstractions;
using Application.DTOs;
using Application.Handlers.DeleteStudent;
using Application.Handlers.GetStudent;
using Application.Handlers.RegisterStudent;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register concrete handlers
        services.AddScoped<RegisterStudentHandler>();
        services.AddScoped<GetStudentHandler>();
        services.AddScoped<DeleteStudentHandler>();

        // Register authorized wrappers (decorator pattern)
        services.AddScoped<IHandler<RegisterStudentCommand, StudentDto>>(sp =>
            new AuthorizedHandler<RegisterStudentCommand, StudentDto>(
                sp.GetRequiredService<RegisterStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        services.AddScoped<IHandler<GetStudentQuery, StudentDto>>(sp =>
            new AuthorizedHandler<GetStudentQuery, StudentDto>(
                sp.GetRequiredService<GetStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        services.AddScoped<IHandler<DeleteStudentCommand>>(sp =>
            new AuthorizedHandler<DeleteStudentCommand>(
                sp.GetRequiredService<DeleteStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        return services;
    }
}
