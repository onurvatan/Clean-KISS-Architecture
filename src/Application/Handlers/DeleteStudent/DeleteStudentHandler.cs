using Application.Abstractions;
using Application.Extensions;
using Application.Services;
using Domain.Interfaces;

namespace Application.Handlers.DeleteStudent;

public class DeleteStudentHandler : IHandler<DeleteStudentCommand>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteStudentHandler(
        IStudentRepository repository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteStudentCommand command, CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (student is null)
            return Result.NotFound("Student not found");

        await _repository.DeleteAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await _cache.RemoveAsync(CacheKeys.Student(command.Id), cancellationToken);
        await _cache.RemoveAsync(CacheKeys.AllStudents, cancellationToken);

        return Result.NoContent();
    }
}
