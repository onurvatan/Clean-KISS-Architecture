using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Students.FindAsync([id], cancellationToken);

    public async Task<Student?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Students
            .FirstOrDefaultAsync(s => s.Email.Value == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Students
            .AnyAsync(s => s.Email.Value == normalizedEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Students.ToListAsync(cancellationToken);

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
        => await _context.Students.AddAsync(student, cancellationToken);

    public Task UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        _context.Students.Update(student);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Student student, CancellationToken cancellationToken = default)
    {
        _context.Students.Remove(student);
        return Task.CompletedTask;
    }
}
