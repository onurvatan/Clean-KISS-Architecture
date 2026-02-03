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
        // Handlers with return value
        services.AddScoped<IHandler<RegisterStudentCommand, StudentDto>, RegisterStudentHandler>();
        services.AddScoped<IHandler<GetStudentQuery, StudentDto>, GetStudentHandler>();

        // Handlers without return value
        services.AddScoped<IHandler<DeleteStudentCommand>, DeleteStudentHandler>();

        return services;
    }
}
