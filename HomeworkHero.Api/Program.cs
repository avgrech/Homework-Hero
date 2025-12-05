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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HomeworkHeroContext>();
    db.Database.EnsureCreated();
}

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

students.MapPost("/{id:int}/flag", async (int id, FlagStudentRequest request, HomeworkHeroContext db) =>
{
    var student = await db.Students.FindAsync(id);
    if (student is null)
    {
        return Results.NotFound();
    }

    student.IsChatBlocked = request.IsChatBlocked;
    await db.SaveChangesAsync();
    return Results.Ok(student);
});

students.MapGet("/{id:int}/prompts", async (int id, int? homeworkId, HomeworkHeroContext db) =>
{
    var promptsQuery = db.StudentPrompts
        .Where(p => p.StudentId == id);

    if (homeworkId.HasValue)
    {
        promptsQuery = promptsQuery.Where(p => p.HomeworkItemId == homeworkId);
    }

    var prompts = await promptsQuery
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new StudentPromptDto(p.Id, p.PromptText, p.ResponseText, p.CreatedAt))
        .ToListAsync();

    return prompts;
});

var teachers = app.MapGroup("/api/teachers");
teachers.MapGet("/", async (HomeworkHeroContext db) => await db.Teachers.ToListAsync());
teachers.MapPost("/", async (Teacher teacher, HomeworkHeroContext db) =>
{
    db.Teachers.Add(teacher);
    await db.SaveChangesAsync();
    return Results.Created($"/api/teachers/{teacher.Id}", teacher);
});

teachers.MapGet("/{id:int}/groups", async (int id, HomeworkHeroContext db) =>
{
    var groups = await db.StudentTeachers
        .Include(st => st.Student)
        .Where(st => st.TeacherId == id)
        .GroupBy(st => st.GroupId)
        .Select(g => new GroupSummaryDto(
            g.Key,
            g.Where(st => st.Student != null)
             .Select(st => new StudentSummaryDto
             {
                 Id = st.StudentId,
                 FirstName = st.Student!.FirstName,
                 LastName = st.Student.LastName,
                 IsChatBlocked = st.Student.IsChatBlocked
             })
             .ToList()))
        .ToListAsync();

    return groups;
});

teachers.MapGet("/{teacherId:int}/students/{studentId:int}/homework", async (int teacherId, int studentId, HomeworkHeroContext db) =>
{
    var groupIds = await db.StudentTeachers
        .Where(st => st.StudentId == studentId && st.TeacherId == teacherId)
        .Select(st => st.GroupId)
        .Distinct()
        .ToListAsync();

    var summaries = await db.HomeworkItems
        .Where(h => h.TeacherId == teacherId &&
            (h.AssignedStudentId == studentId || (h.AssignedGroupId != null && groupIds.Contains(h.AssignedGroupId))))
        .Select(h => new
        {
            Homework = h,
            Result = h.Results.FirstOrDefault(r => r.StudentId == studentId)
        })
        .AsNoTracking()
        .ToListAsync();

    var now = DateOnly.FromDateTime(DateTime.UtcNow);
    var response = summaries
        .Select(h => new StudentHomeworkSummaryDto(
            h.Homework.Id,
            h.Homework.Title,
            h.Homework.Subject,
            h.Homework.DueDate,
            h.Result != null ? "submitted" : h.Homework.DueDate < now ? "overdue" : "pending",
            h.Result?.SubmittedAt))
        .OrderBy(h => h.DueDate)
        .ToList();

    return response;
});

teachers.MapGet("/{teacherId:int}/notifications", async (int teacherId, HomeworkHeroContext db) =>
{
    var notifications = await db.Notifications
        .Where(n => n.TeacherId == teacherId)
        .OrderByDescending(n => n.CreatedAt)
        .Select(n => new NotificationDto(n.Id, n.Message, n.CreatedAt, n.IsRead, n.StudentId, n.HomeworkItemId))
        .ToListAsync();

    return notifications;
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
homework.MapGet("/{id:int}/details/{studentId:int}", async (int id, int studentId, HomeworkHeroContext db) =>
{
    var item = await db.HomeworkItems
        .Include(h => h.Prompts.Where(p => p.StudentId == studentId))
        .Include(h => h.Results.Where(r => r.StudentId == studentId))
        .FirstOrDefaultAsync(h => h.Id == id);

    if (item is null)
    {
        return Results.NotFound();
    }

    var result = item.Results.FirstOrDefault(r => r.StudentId == studentId);
    var now = DateOnly.FromDateTime(DateTime.UtcNow);
    var status = result != null ? "submitted" : item.DueDate < now ? "overdue" : "pending";

    var detail = new StudentHomeworkDetailDto(
        item.Id,
        item.Title,
        item.Subject,
        item.DueDate,
        status,
        item.TextContent,
        result?.ResultText,
        result?.SubmittedAt,
        item.Prompts
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new StudentPromptDto(p.Id, p.PromptText, p.ResponseText, p.CreatedAt))
            .ToList());

    return Results.Ok(detail);
});
homework.MapPost("/", async (HomeworkItem item, HomeworkHeroContext db) =>
{
    if (string.IsNullOrWhiteSpace(item.Subject))
    {
        return Results.BadRequest("Subject is required");
    }

    if (item.DueDate == default)
    {
        return Results.BadRequest("Due date is required");
    }

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
    var homeworkItem = await db.HomeworkItems.FirstOrDefaultAsync(h => h.Id == id);
    if (homeworkItem is not null)
    {
        db.Notifications.Add(new Notification
        {
            TeacherId = homeworkItem.TeacherId,
            StudentId = result.StudentId,
            HomeworkItemId = homeworkItem.Id,
            Message = $"Homework '{homeworkItem.Title}' submitted by student {result.StudentId}"
        });
    }
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
