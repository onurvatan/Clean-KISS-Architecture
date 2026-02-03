using Application.DTOs;
using Domain.Entities;

namespace Application.Extensions;

public static class StudentExtensions
{
    public static StudentDto ToDto(this Student student)
        => new(student.Id, student.Name.Value, student.Email.Value, student.CreatedAt);
}
