namespace Application.Extensions;

public static class CacheKeys
{
    public static string Student(Guid id) => $"student:{id}";
    public static string StudentByEmail(string email) => $"student:email:{email}";
    public static string AllStudents => "students:all";
}
