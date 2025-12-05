using HomeworkHero.Api.Data;
using HomeworkHero.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

public record RegisterUserRequest(
    string Email,
    string Password,
    string DisplayName,
    UserRole Role,
    int? StudentId,
    int? TeacherId,
    List<int>? PermissionIds);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HomeworkHeroContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowClient");

app.UseSwagger();
app.UseSwaggerUI();

var students = app.MapGroup("/api/students");
students.MapGet("/", async (HomeworkHeroContext db) => await db.Students
        .Include(s => s.Conditions)
        .Include(s => s.Enrollments)
        .ToListAsync());

students.MapGet("/{id:int}", async (int id, HomeworkHeroContext db) =>
    await db.Students.Include(s => s.Conditions).ThenInclude(sc => sc.Condition)
                     .Include(s => s.Enrollments)
                     .FirstOrDefaultAsync(s => s.Id == id)
        is Student student
            ? Results.Ok(student)
            : Results.NotFound());

students.MapPost("/", async (Student student, HomeworkHeroContext db) =>
{
    db.Students.Add(student);
    await db.SaveChangesAsync();
    return Results.Created($"/api/students/{student.Id}", student);
});

students.MapPost("/{id:int}/conditions", async (int id, StudentCondition condition, HomeworkHeroContext db) =>
{
    var exists = await db.Students.AnyAsync(s => s.Id == id) &&
                 await db.Conditions.AnyAsync(c => c.Id == condition.ConditionId);
    if (!exists)
    {
        return Results.NotFound();
    }

    condition.StudentId = id;
    db.StudentConditions.Add(condition);
    await db.SaveChangesAsync();
    return Results.Created($"/api/students/{id}/conditions/{condition.ConditionId}", condition);
});

students.MapPost("/{id:int}/teachers", async (int id, StudentTeacher enrollment, HomeworkHeroContext db) =>
{
    var exists = await db.Students.AnyAsync(s => s.Id == id) &&
                 await db.Teachers.AnyAsync(t => t.Id == enrollment.TeacherId);
    if (!exists)
    {
        return Results.NotFound();
    }

    enrollment.StudentId = id;
    db.StudentTeachers.Add(enrollment);
    await db.SaveChangesAsync();
    return Results.Created($"/api/students/{id}/teachers/{enrollment.TeacherId}", enrollment);
});

var teachers = app.MapGroup("/api/teachers");
teachers.MapGet("/", async (HomeworkHeroContext db) => await db.Teachers.ToListAsync());
teachers.MapPost("/", async (Teacher teacher, HomeworkHeroContext db) =>
{
    db.Teachers.Add(teacher);
    await db.SaveChangesAsync();
    return Results.Created($"/api/teachers/{teacher.Id}", teacher);
});

var conditions = app.MapGroup("/api/conditions");
conditions.MapGet("/", async (HomeworkHeroContext db) => await db.Conditions.ToListAsync());
conditions.MapPost("/", async (Condition condition, HomeworkHeroContext db) =>
{
    db.Conditions.Add(condition);
    await db.SaveChangesAsync();
    return Results.Created($"/api/conditions/{condition.Id}", condition);
});

var homework = app.MapGroup("/api/homework");
homework.MapGet("/", async (HomeworkHeroContext db) => await db.HomeworkItems.ToListAsync());
homework.MapGet("/{id:int}", async (int id, HomeworkHeroContext db) =>
    await db.HomeworkItems.FirstOrDefaultAsync(h => h.Id == id)
        is HomeworkItem item
            ? Results.Ok(item)
            : Results.NotFound());
homework.MapPost("/", async (HomeworkItem item, HomeworkHeroContext db) =>
{
    if (!await db.Teachers.AnyAsync(t => t.Id == item.TeacherId))
    {
        return Results.BadRequest("Teacher not found");
    }

    db.HomeworkItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/homework/{item.Id}", item);
});

homework.MapPost("/{id:int}/actions", async (int id, StudentAction action, HomeworkHeroContext db) =>
{
    if (!await db.HomeworkItems.AnyAsync(h => h.Id == id) ||
        !await db.Students.AnyAsync(s => s.Id == action.StudentId))
    {
        return Results.NotFound();
    }

    action.HomeworkItemId = id;
    db.StudentActions.Add(action);
    await db.SaveChangesAsync();
    return Results.Created($"/api/homework/{id}/actions/{action.Id}", action);
});

homework.MapPost("/{id:int}/prompts", async (int id, StudentPrompt prompt, HomeworkHeroContext db) =>
{
    if (!await db.HomeworkItems.AnyAsync(h => h.Id == id) ||
        !await db.Students.AnyAsync(s => s.Id == prompt.StudentId))
    {
        return Results.NotFound();
    }

    prompt.HomeworkItemId = id;
    db.StudentPrompts.Add(prompt);
    await db.SaveChangesAsync();
    return Results.Created($"/api/homework/{id}/prompts/{prompt.Id}", prompt);
});

homework.MapPost("/{id:int}/results", async (int id, HomeworkResult result, HomeworkHeroContext db) =>
{
    if (!await db.HomeworkItems.AnyAsync(h => h.Id == id) ||
        !await db.Students.AnyAsync(s => s.Id == result.StudentId))
    {
        return Results.NotFound();
    }

    result.HomeworkItemId = id;
    db.HomeworkResults.Add(result);
    await db.SaveChangesAsync();
    return Results.Created($"/api/homework/{id}/results/{result.Id}", result);
});

var permissions = app.MapGroup("/api/permissions");
permissions.MapGet("/", async (HomeworkHeroContext db) => await db.Permissions.ToListAsync());
permissions.MapPost("/", async (Permission permission, HomeworkHeroContext db) =>
{
    db.Permissions.Add(permission);
    await db.SaveChangesAsync();
    return Results.Created($"/api/permissions/{permission.Id}", permission);
});

var users = app.MapGroup("/api/users");
users.MapGet("/", async (HomeworkHeroContext db) => await db.Users
        .Include(u => u.Permissions)
        .ThenInclude(up => up.Permission)
        .ToListAsync());

users.MapPost("/", async (RegisterUserRequest request, HomeworkHeroContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Email == request.Email))
    {
        return Results.Conflict("Email already registered");
    }

    if (request.StudentId.HasValue && !await db.Students.AnyAsync(s => s.Id == request.StudentId))
    {
        return Results.BadRequest("Student not found");
    }

    if (request.TeacherId.HasValue && !await db.Teachers.AnyAsync(t => t.Id == request.TeacherId))
    {
        return Results.BadRequest("Teacher not found");
    }

    var user = new User
    {
        Email = request.Email,
        DisplayName = request.DisplayName,
        PasswordHash = HashPassword(request.Password),
        Role = request.Role,
        StudentId = request.StudentId,
        TeacherId = request.TeacherId
    };

    db.Users.Add(user);

    if (request.PermissionIds?.Any() == true)
    {
        var validPermissions = await db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync();

        foreach (var permission in validPermissions)
        {
            db.UserPermissions.Add(new UserPermission
            {
                User = user,
                Permission = permission
            });
        }
    }

    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

var auth = app.MapGroup("/api/auth");
auth.MapPost("/login", async (LoginRequest login, HomeworkHeroContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
    if (user is null)
    {
        return Results.NotFound(new LoginResponse(false, null, "User not found"));
    }

    if (!VerifyPassword(login.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new LoginResponse(true, user.Role.ToString(), "Authenticated"));
});

app.MapGet("/api/health", () => Results.Ok("Healthy"));

app.Run();

static string HashPassword(string password)
{
    using var sha = SHA256.Create();
    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
    return Convert.ToBase64String(bytes);
}

static bool VerifyPassword(string password, string hash)
{
    return HashPassword(password) == hash;
}

app.Run();
