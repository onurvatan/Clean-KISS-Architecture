using Application.Abstractions;
using Application.DTOs;
using Application.Extensions;
using Application.Services;
using Domain.Interfaces;

namespace Application.Handlers.GetStudent;

public class GetStudentHandler : IHandler<GetStudentQuery, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly ICacheService _cache;

    public GetStudentHandler(IStudentRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<Result<StudentDto>> Handle(GetStudentQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Student(query.Id);

        var dto = await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            var student = await _repository.GetByIdAsync(query.Id, cancellationToken);
            return student?.ToDto();
        }, TimeSpan.FromMinutes(10), cancellationToken);

        if (dto is null)
            return Result<StudentDto>.NotFound("Student not found");

        return Result<StudentDto>.Success(dto);
    }
}
