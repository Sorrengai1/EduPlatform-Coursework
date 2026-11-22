using EduPlatform.Data;
using EduPlatform.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduPlatform.Tests;

public class SqliteIntegrationTests
{
    [Fact]
    public async Task Migrations_CreateExpectedTables()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);

        await db.Database.MigrateAsync();

        var tables = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table'";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        Assert.Contains("Courses", tables);
        Assert.Contains("Students", tables);
        Assert.Contains("Attempts", tables);
        Assert.Contains("Progresses", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Course_CanBeSavedAndReadFromSqlite()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.MigrateAsync();

            db.Courses.Add(new Course
            {
                Title = "Integration Test Course",
                Description = "Course created to test SQLite persistence."
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var course = await db.Courses.SingleAsync();

            Assert.Equal("Integration Test Course", course.Title);
            Assert.True(course.Id > 0);
        }
    }
}
