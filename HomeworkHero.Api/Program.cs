using HomeworkHero.Api.Data;
using HomeworkHero.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HomeworkHeroContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Homework Hero API",
        Version = "v1"
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HomeworkHeroContext>();
    db.Database.EnsureCreated();

    if (!db.Users.Any(u => u.Role == UserRole.Admin))
    {
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@example.com",
            DisplayName = "System Administrator",
            Role = UserRole.Admin,
            PasswordHash = HashPassword("password"),
            MustResetPassword = true
        };

        db.Users.Add(adminUser);
        db.SaveChanges();
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowClient");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Homework Hero API v1");
});

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
    await EnsureUserForStudentAsync(student, db);
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

students.MapPost("/flag-all", async (FlagStudentRequest request, HomeworkHeroContext db) =>
{
    var studentsToUpdate = await db.Students.ToListAsync();
    foreach (var student in studentsToUpdate)
    {
        student.IsChatBlocked = request.IsChatBlocked;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new
    {
        Updated = studentsToUpdate.Count,
        request.IsChatBlocked
    });
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
    await EnsureUserForTeacherAsync(teacher, db);
    return Results.Created($"/api/teachers/{teacher.Id}", teacher);
});

teachers.MapPut("/{id:int}", async (int id, Teacher updatedTeacher, HomeworkHeroContext db) =>
{
    var existing = await db.Teachers.FindAsync(id);
    if (existing is null)
    {
        return Results.NotFound();
    }

    existing.FirstName = updatedTeacher.FirstName;
    existing.LastName = updatedTeacher.LastName;
    existing.Email = updatedTeacher.Email;
    existing.Details = updatedTeacher.Details;

    await db.SaveChangesAsync();
    return Results.Ok(existing);
});

teachers.MapDelete("/{id:int}", async (int id, HomeworkHeroContext db) =>
{
    var existing = await db.Teachers.FindAsync(id);
    if (existing is null)
    {
        return Results.NotFound();
    }

    db.Teachers.Remove(existing);
    await db.SaveChangesAsync();
    return Results.NoContent();
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

teachers.MapGet("/{id:int}/students", async (int id, HomeworkHeroContext db) =>
{
    var students = await db.StudentTeachers
        .Include(st => st.Student)
        .Where(st => st.TeacherId == id && st.Student != null)
        .Select(st => st.Student!)
        .GroupBy(s => s.Id)
        .Select(g => g.First())
        .OrderBy(s => s.LastName)
        .ThenBy(s => s.FirstName)
        .ToListAsync();

    return students;
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
homework.MapGet("/student/{studentId:int}", async (int studentId, HomeworkHeroContext db) =>
{
    var groupIds = await db.StudentTeachers
        .Where(st => st.StudentId == studentId)
        .Select(st => st.GroupId)
        .Distinct()
        .ToListAsync();

    var assignedHomework = await db.HomeworkItems
        .Where(h => h.AssignedStudentId == studentId || (h.AssignedGroupId != null && groupIds.Contains(h.AssignedGroupId)))
        .ToListAsync();

    return assignedHomework;
});
homework.MapGet("/teacher/{teacherId:int}", async (int teacherId, HomeworkHeroContext db) =>
    await db.HomeworkItems.Where(h => h.TeacherId == teacherId).ToListAsync());
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

var uploads = app.MapGroup("/api/uploads");
uploads.MapPost("/", async (IFormFile? file, IWebHostEnvironment env) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    var uploadDirectory = Path.Combine(webRoot, "uploads");
    Directory.CreateDirectory(uploadDirectory);

    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var filePath = Path.Combine(uploadDirectory, fileName);

    await using (var stream = File.Create(filePath))
    {
        await file.CopyToAsync(stream);
    }

    var fileUrl = $"/uploads/{fileName}";
    return Results.Ok(new { Url = fileUrl });
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
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.Conflict("Username already registered");
    }

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
        Username = request.Username,
        Email = request.Email,
        DisplayName = request.DisplayName,
        PasswordHash = HashPassword(request.Password),
        Role = request.Role,
        StudentId = request.StudentId,
        TeacherId = request.TeacherId,
        MustResetPassword = request.MustResetPassword
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

var admin = app.MapGroup("/api/admin");

admin.MapGet("/classrooms", async (HomeworkHeroContext db) =>
{
    var classrooms = await db.StudentTeachers
        .Include(st => st.Teacher)
        .Include(st => st.Student)
        .GroupBy(st => new
        {
            st.GroupId,
            st.TeacherId,
            st.Teacher!.FirstName,
            st.Teacher.LastName
        })
        .Select(g => new ClassroomSummaryDto(
            g.Key.GroupId,
            g.Key.TeacherId,
            $"{g.Key.FirstName} {g.Key.LastName}",
            g.Where(st => st.Student != null)
                .Select(st => new StudentSummaryDto
                {
                    Id = st.StudentId,
                    FirstName = st.Student!.FirstName,
                    LastName = st.Student.LastName,
                    IsChatBlocked = st.Student.IsChatBlocked
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList()))
        .OrderBy(c => c.GroupId)
        .ThenBy(c => c.TeacherName)
        .ToListAsync();

    return classrooms;
});

admin.MapPost("/classrooms/assign", async (ClassroomAssignmentRequest request, HomeworkHeroContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.GroupId) || request.StudentIds is null || !request.StudentIds.Any())
    {
        return Results.BadRequest("A group name, teacher, and at least one student are required.");
    }

    var teacher = await db.Teachers.FindAsync(request.TeacherId);
    if (teacher is null)
    {
        return Results.NotFound("Teacher not found");
    }

    var requestedStudentIds = request.StudentIds.Distinct().ToList();
    var students = await db.Students
        .Where(s => requestedStudentIds.Contains(s.Id))
        .ToListAsync();

    var missingStudents = requestedStudentIds.Except(students.Select(s => s.Id)).ToList();
    if (students.Count == 0)
    {
        return Results.NotFound("No valid students were found for this assignment.");
    }

    var now = DateOnly.FromDateTime(DateTime.UtcNow);

    var existingEnrollments = await db.StudentTeachers
        .Where(st => requestedStudentIds.Contains(st.StudentId) && st.GroupId == request.GroupId)
        .ToListAsync();

    var toRemove = existingEnrollments
        .Where(st => st.TeacherId != request.TeacherId)
        .ToList();

    if (toRemove.Any())
    {
        db.StudentTeachers.RemoveRange(toRemove);
    }

    foreach (var student in students)
    {
        var alreadyAssigned = existingEnrollments.Any(st => st.StudentId == student.Id && st.TeacherId == request.TeacherId);
        if (!alreadyAssigned)
        {
            db.StudentTeachers.Add(new StudentTeacher
            {
                StudentId = student.Id,
                TeacherId = request.TeacherId,
                GroupId = request.GroupId,
                StartDate = now
            });
        }
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Assigned = students.Count - (existingEnrollments.Count - toRemove.Count),
        Reassigned = toRemove.Count,
        MissingStudents = missingStudents
    });
});

admin.MapGet("/bulk-template", () =>
{
    var template = new StringBuilder();
    template.AppendLine("type,first_name,last_name,email,date_of_birth,details,group_id,teacher_email");
    template.AppendLine("teacher,Alice,Anderson,alice.anderson@example.com,,Math lead,");
    template.AppendLine("student,Bob,Brown,bob.brown@example.com,2013-09-01,Needs reading support,ReadingGroupA,alice.anderson@example.com");
    template.AppendLine("student,Casey,Clark,casey.clark@example.com,2013-02-14,,ReadingGroupA,alice.anderson@example.com");

    var bytes = Encoding.UTF8.GetBytes(template.ToString());
    return Results.File(bytes, "text/csv", "bulk-import-template.csv");
});

admin.MapPost("/bulk-import", async (HttpRequest request, HomeworkHeroContext db) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("File upload expected");
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null)
    {
        return Results.BadRequest("CSV file is required");
    }

    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
    var content = await reader.ReadToEndAsync();

    var lines = content
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    var errors = new List<string>();
    if (lines.Count <= 1)
    {
        return Results.Ok(new BulkImportResult(0, 0, 0, new List<string> { "No data rows were provided" }));
    }

    var teachersCreated = 0;
    var studentsCreated = 0;
    var enrollmentsCreated = 0;

    for (var i = 1; i < lines.Count; i++)
    {
        var line = lines[i];
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var columns = line.Split(',', StringSplitOptions.TrimEntries);
        string GetColumn(int index) => index < columns.Length ? columns[index] : string.Empty;

        var type = GetColumn(0).ToLowerInvariant();
        var firstName = GetColumn(1);
        var lastName = GetColumn(2);
        var email = GetColumn(3);

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(email))
        {
            errors.Add($"Row {i + 1}: type and email are required");
            continue;
        }

        switch (type)
        {
            case "teacher":
                if (!await db.Teachers.AnyAsync(t => t.Email == email))
                {
                    db.Teachers.Add(new Teacher
                    {
                        FirstName = string.IsNullOrWhiteSpace(firstName) ? "Teacher" : firstName,
                        LastName = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
                        Email = email,
                        Details = GetColumn(5)
                    });
                    teachersCreated++;
                }
                break;

            case "student":
                var teacherEmail = GetColumn(7);
                var groupId = string.IsNullOrWhiteSpace(GetColumn(6)) ? "Ungrouped" : GetColumn(6);
                if (string.IsNullOrWhiteSpace(teacherEmail))
                {
                    errors.Add($"Row {i + 1}: teacher_email is required for student rows");
                    continue;
                }

                var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Email == teacherEmail);
                if (teacher is null)
                {
                    errors.Add($"Row {i + 1}: teacher '{teacherEmail}' was not found. Add a teacher row first.");
                    continue;
                }

                var student = await db.Students.FirstOrDefaultAsync(s => s.Email == email);
                if (student is null)
                {
                    var dobText = GetColumn(4);
                    var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10));
                    if (!string.IsNullOrWhiteSpace(dobText) && DateOnly.TryParse(dobText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDob))
                    {
                        dob = parsedDob;
                    }

                    student = new Student
                    {
                        FirstName = string.IsNullOrWhiteSpace(firstName) ? "Student" : firstName,
                        LastName = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
                        Email = email,
                        DateOfBirth = dob,
                        Details = GetColumn(5)
                    };

                    db.Students.Add(student);
                    studentsCreated++;
                }

                var existingEnrollment = await db.StudentTeachers.AnyAsync(st => st.StudentId == student.Id && st.TeacherId == teacher.Id && st.GroupId == groupId);
                if (!existingEnrollment)
                {
                    db.StudentTeachers.Add(new StudentTeacher
                    {
                        Student = student,
                        Teacher = teacher,
                        GroupId = groupId,
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
                    });
                    enrollmentsCreated++;
                }

                break;

            default:
                errors.Add($"Row {i + 1}: unsupported type '{type}'. Use 'teacher' or 'student'.");
                break;
        }
    }

    await db.SaveChangesAsync();
    await EnsureUsersForTeachersAsync(db);
    await EnsureUsersForStudentsAsync(db);

    return Results.Ok(new BulkImportResult(teachersCreated, studentsCreated, enrollmentsCreated, errors));
});

var auth = app.MapGroup("/api/auth");
auth.MapPost("/login", async (LoginRequest login, HomeworkHeroContext db) =>
{
    var identifier = login.Identifier.ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u =>
        u.Username.ToLower() == identifier || u.Email.ToLower() == identifier);
    if (user is null)
    {
        return Results.NotFound(new LoginResponse(false, null, "User not found"));
    }

    if (!VerifyPassword(login.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new LoginResponse(
        true,
        user.Role.ToString(),
        "Authenticated",
        user.MustResetPassword,
        user.Email,
        user.StudentId,
        user.TeacherId));
});

auth.MapPost("/reset-password", async (ResetPasswordRequest reset, HomeworkHeroContext db) =>
{
    var identifier = reset.Identifier.ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u =>
        u.Username.ToLower() == identifier || u.Email.ToLower() == identifier);

    if (user is null)
    {
        return Results.NotFound("User not found");
    }

    if (!VerifyPassword(reset.CurrentPassword, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    user.PasswordHash = HashPassword(reset.NewPassword);
    user.MustResetPassword = false;
    await db.SaveChangesAsync();

    return Results.Ok(new { Message = "Password updated" });
});

app.MapGet("/api/health", () => Results.Ok("Healthy"));

app.Run();

static async Task EnsureUsersForStudentsAsync(HomeworkHeroContext db)
{
    var students = await db.Students.ToListAsync();
    foreach (var student in students)
    {
        await EnsureUserForStudentAsync(student, db);
    }
}

static async Task EnsureUsersForTeachersAsync(HomeworkHeroContext db)
{
    var teachers = await db.Teachers.ToListAsync();
    foreach (var teacher in teachers)
    {
        await EnsureUserForTeacherAsync(teacher, db);
    }
}

static async Task EnsureUserForStudentAsync(Student student, HomeworkHeroContext db)
{
    if (student.Id == 0)
    {
        return;
    }

    var normalizedEmail = student.Email.ToLowerInvariant();
    var existingUser = await db.Users.FirstOrDefaultAsync(u =>
        u.StudentId == student.Id ||
        u.Email.ToLower() == normalizedEmail ||
        u.Username.ToLower() == normalizedEmail);

    if (existingUser is not null)
    {
        return;
    }

    var displayName = $"{student.FirstName} {student.LastName}".Trim();
    if (string.IsNullOrWhiteSpace(displayName))
    {
        displayName = student.Email;
    }

    db.Users.Add(new User
    {
        Username = student.Email,
        Email = student.Email,
        DisplayName = displayName,
        Role = UserRole.Student,
        PasswordHash = HashPassword("password"),
        MustResetPassword = true,
        StudentId = student.Id
    });

    await db.SaveChangesAsync();
}

static async Task EnsureUserForTeacherAsync(Teacher teacher, HomeworkHeroContext db)
{
    if (teacher.Id == 0)
    {
        return;
    }

    var normalizedEmail = teacher.Email.ToLowerInvariant();
    var existingUser = await db.Users.FirstOrDefaultAsync(u =>
        u.TeacherId == teacher.Id ||
        u.Email.ToLower() == normalizedEmail ||
        u.Username.ToLower() == normalizedEmail);

    if (existingUser is not null)
    {
        return;
    }

    var displayName = $"{teacher.FirstName} {teacher.LastName}".Trim();
    if (string.IsNullOrWhiteSpace(displayName))
    {
        displayName = teacher.Email;
    }

    db.Users.Add(new User
    {
        Username = teacher.Email,
        Email = teacher.Email,
        DisplayName = displayName,
        Role = UserRole.Teacher,
        PasswordHash = HashPassword("password"),
        MustResetPassword = true,
        TeacherId = teacher.Id
    });

    await db.SaveChangesAsync();
}

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

public record RegisterUserRequest(
    string Username,
    string Email,
    string Password,
    string DisplayName,
    UserRole Role,
    int? StudentId,
    int? TeacherId,
    List<int>? PermissionIds,
    bool MustResetPassword = false);
