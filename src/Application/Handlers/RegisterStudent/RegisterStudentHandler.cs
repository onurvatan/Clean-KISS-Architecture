using Application.Abstractions;
using Application.DTOs;
using Application.Extensions;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

namespace Application.Handlers.RegisterStudent;

public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public RegisterStudentHandler(
        IStudentRepository repository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command, CancellationToken cancellationToken = default)
    {
        // Business validation: check uniqueness
        var exists = await _repository.ExistsByEmailAsync(command.Email, cancellationToken);
        if (exists)
            return Result<StudentDto>.Conflict("A student with this email already exists");

        // Value objects validate format (throw if invalid)
        var name = new Name(command.Name);
        var email = new Email(command.Email);
        var student = new Student(name, email);

        await _repository.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate list cache
        await _cache.RemoveAsync(CacheKeys.AllStudents, cancellationToken);

        return Result<StudentDto>.Created(student.ToDto());
    }
}
