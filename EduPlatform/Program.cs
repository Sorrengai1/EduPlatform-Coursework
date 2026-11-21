using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;

var builder = WebApplication.CreateBuilder(args);

// Реєстрація MVC-контролерів і Razor-представлень.
builder.Services.AddControllersWithViews();

// SQLite через Entity Framework Core.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Не задано рядок підключення DefaultConnection.")));

// Сховище для сесій.
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Ініціалізація БД без видалення наявних даних.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();

        // Тимчасова сумісність зі старою схемою SQLite.
        // Повноцінні міграції EF Core налаштуємо окремо після перевірки DbContext.
        var connection = context.Database.GetDbConnection();
        connection.Open();

        try
        {
            // Додаємо відсутні стовпці до наявних таблиць, не видаляючи записи.
            void EnsureColumn(string table, string column, string definition)
            {
                bool tableExists;
                using (var tableCheck = connection.CreateCommand())
                {
                    tableCheck.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master " +
                        "WHERE type = 'table' AND name = $table;";
                    var parameter = tableCheck.CreateParameter();
                    parameter.ParameterName = "$table";
                    parameter.Value = table;
                    tableCheck.Parameters.Add(parameter);
                    tableExists = Convert.ToInt64(tableCheck.ExecuteScalar()) > 0;
                }

                if (!tableExists)
                    throw new InvalidOperationException(
                        $"У базі даних відсутня таблиця {table}. Перевірте схему бази даних.");

                bool hasColumn = false;
                using (var check = connection.CreateCommand())
                {
                    check.CommandText = $"PRAGMA table_info('{table}');";
                    using var reader = check.ExecuteReader();
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), column,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            hasColumn = true;
                            break;
                        }
                    }
                }

                if (!hasColumn)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                    alter.ExecuteNonQuery();
                    logger.LogInformation("До таблиці {Table} додано стовпець {Column}.", table, column);
                }
            }

            EnsureColumn("Modules", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("Attempts", "IsPractice", "INTEGER NOT NULL DEFAULT 0");
        }
        finally
        {
            connection.Close();
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Помилка ініціалізації бази даних.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
