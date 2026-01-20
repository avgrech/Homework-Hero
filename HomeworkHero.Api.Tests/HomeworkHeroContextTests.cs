using HomeworkHero.Api.Data;
using HomeworkHero.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomeworkHero.Api.Tests;

public class HomeworkHeroContextTests
{
    private static HomeworkHeroContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HomeworkHeroContext>()
            .UseInMemoryDatabase($"HomeworkHero-{Guid.NewGuid()}")
            .Options;

        return new HomeworkHeroContext(options);
    }

    [Fact]
    public void StudentCondition_has_composite_key_and_relationships()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(StudentCondition));

        Assert.NotNull(entityType);
        Assert.Equal(new[] { nameof(StudentCondition.StudentId), nameof(StudentCondition.ConditionId) },
            entityType!.FindPrimaryKey()!.Properties.Select(p => p.Name));

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.Contains(foreignKeys, fk => fk.PrincipalEntityType.ClrType == typeof(Student));
        Assert.Contains(foreignKeys, fk => fk.PrincipalEntityType.ClrType == typeof(Condition));
    }

    [Fact]
    public void StudentTeacher_has_composite_key()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(StudentTeacher));

        Assert.NotNull(entityType);
        Assert.Equal(new[] { nameof(StudentTeacher.StudentId), nameof(StudentTeacher.TeacherId), nameof(StudentTeacher.GroupId) },
            entityType!.FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Classroom_group_id_is_unique_and_teacher_is_optional()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Classroom));

        Assert.NotNull(entityType);
        var groupIndex = entityType!.GetIndexes().SingleOrDefault(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(Classroom.GroupId));

        Assert.NotNull(groupIndex);
        Assert.True(groupIndex!.IsUnique);

        var teacherForeignKey = entityType.GetForeignKeys()
            .SingleOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Teacher));

        Assert.NotNull(teacherForeignKey);
        Assert.False(teacherForeignKey!.IsRequired);
    }

    [Fact]
    public void HomeworkItem_relationships_use_restrict_delete_behavior()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(HomeworkItem));

        Assert.NotNull(entityType);
        var foreignKeys = entityType!.GetForeignKeys().ToList();

        var teacherFk = foreignKeys.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Teacher));
        var studentFk = foreignKeys.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Student));

        Assert.Equal(DeleteBehavior.Restrict, teacherFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, studentFk.DeleteBehavior);
    }

    [Fact]
    public void User_has_unique_indexes_on_username_and_email()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(User));

        Assert.NotNull(entityType);
        var indexes = entityType!.GetIndexes();

        var usernameIndex = indexes.SingleOrDefault(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(User.Username));
        var emailIndex = indexes.SingleOrDefault(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(User.Email));

        Assert.NotNull(usernameIndex);
        Assert.NotNull(emailIndex);
        Assert.True(usernameIndex!.IsUnique);
        Assert.True(emailIndex!.IsUnique);
    }

    [Fact]
    public void Notification_delete_behaviors_are_configured()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Notification));

        Assert.NotNull(entityType);
        var foreignKeys = entityType!.GetForeignKeys();

        var teacherFk = foreignKeys.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Teacher));
        var studentFk = foreignKeys.Single(fk => fk.PrincipalEntityType.ClrType == typeof(Student));
        var homeworkFk = foreignKeys.Single(fk => fk.PrincipalEntityType.ClrType == typeof(HomeworkItem));

        Assert.Equal(DeleteBehavior.Cascade, teacherFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, studentFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, homeworkFk.DeleteBehavior);
    }
}
