using HomeworkHero.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeworkHero.Api.Data;

public class HomeworkHeroContext : DbContext
{
    public HomeworkHeroContext(DbContextOptions<HomeworkHeroContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<StudentCondition> StudentConditions => Set<StudentCondition>();
    public DbSet<StudentTeacher> StudentTeachers => Set<StudentTeacher>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<HomeworkItem> HomeworkItems => Set<HomeworkItem>();
    public DbSet<StudentAction> StudentActions => Set<StudentAction>();
    public DbSet<StudentPrompt> StudentPrompts => Set<StudentPrompt>();
    public DbSet<HomeworkResult> HomeworkResults => Set<HomeworkResult>();
    public DbSet<StudentCorrection> StudentCorrections => Set<StudentCorrection>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Paramiter> Paramiters => Set<Paramiter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StudentCondition>()
            .HasKey(sc => new { sc.StudentId, sc.ConditionId });

        modelBuilder.Entity<StudentCondition>()
            .HasOne(sc => sc.Student)
            .WithMany(s => s.Conditions)
            .HasForeignKey(sc => sc.StudentId);

        modelBuilder.Entity<StudentCondition>()
            .HasOne(sc => sc.Condition)
            .WithMany(c => c.StudentConditions)
            .HasForeignKey(sc => sc.ConditionId);

        modelBuilder.Entity<StudentTeacher>()
            .HasKey(st => new { st.StudentId, st.TeacherId, st.GroupId });

        modelBuilder.Entity<StudentTeacher>()
            .HasOne(st => st.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(st => st.StudentId);

        modelBuilder.Entity<StudentTeacher>()
            .HasOne(st => st.Teacher)
            .WithMany(t => t.Students)
            .HasForeignKey(st => st.TeacherId);

        modelBuilder.Entity<Classroom>()
            .HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => c.TeacherId)
            .IsRequired(false);

        modelBuilder.Entity<Classroom>()
            .HasIndex(c => c.GroupId)
            .IsUnique();

        modelBuilder.Entity<HomeworkItem>()
            .HasOne(h => h.Teacher)
            .WithMany(t => t.Homework)
            .HasForeignKey(h => h.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkItem>()
            .HasOne(h => h.AssignedStudent)
            .WithMany()
            .HasForeignKey(h => h.AssignedStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentCorrection>()
            .HasOne(sc => sc.HomeworkResult)
            .WithMany(r => r.StudentCorrections)
            .HasForeignKey(sc => sc.HomeworkResultId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StudentCorrection>()
            .HasOne(sc => sc.Teacher)
            .WithMany()
            .HasForeignKey(sc => sc.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Paramiter>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Student)
            .WithMany()
            .HasForeignKey(u => u.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Teacher)
            .WithMany()
            .HasForeignKey(u => u.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserPermission>()
            .HasKey(up => new { up.UserId, up.PermissionId });

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.User)
            .WithMany(u => u.Permissions)
            .HasForeignKey(up => up.UserId);

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.Permission)
            .WithMany(p => p.UserPermissions)
            .HasForeignKey(up => up.PermissionId);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Teacher)
            .WithMany()
            .HasForeignKey(n => n.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Student)
            .WithMany()
            .HasForeignKey(n => n.StudentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.HomeworkItem)
            .WithMany()
            .HasForeignKey(n => n.HomeworkItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
