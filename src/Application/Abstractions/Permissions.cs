namespace Application.Abstractions;

public static class Permissions
{
    public static class Students
    {
        public const string View = "students:view";
        public const string Create = "students:create";
        public const string Update = "students:update";
        public const string Delete = "students:delete";
    }

    public static class Courses
    {
        public const string View = "courses:view";
        public const string Create = "courses:create";
        public const string Update = "courses:update";
        public const string Delete = "courses:delete";
    }
}
