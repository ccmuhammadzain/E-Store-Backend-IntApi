using InventoryApi.Data;
using InventoryApi.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer; 
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
// Single Swagger registration with JWT security
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer {token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key missing");
var issuer = builder.Configuration["Jwt:Issuer"]; 
var audience = builder.Configuration["Jwt:Audience"]; 

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = !string.IsNullOrEmpty(issuer),
        ValidateAudience = !string.IsNullOrEmpty(audience),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidIssuer = issuer,
        ValidAudience = audience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrSuper", p => p.RequireRole("Admin", "SuperAdmin"));
    options.AddPolicy("SuperOnly", p => p.RequireRole("SuperAdmin"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            Console.WriteLine("[MIGRATIONS] Applying: " + string.Join(", ", pending));
            await db.Database.MigrateAsync();
            Console.WriteLine("[MIGRATIONS] Done");
        }
        else
        {
            Console.WriteLine("[MIGRATIONS] None pending");
        }

        if (!await db.Users.AnyAsync(u => u.Role == "SuperAdmin"))
        {
            var superPassword = "SuperAdmin#12345"; // TODO: secure secret retrieval
            var hashed = BCrypt.Net.BCrypt.HashPassword(superPassword);
            db.Users.Add(new User
            {
                Username = "superadmin",
                PasswordHash = hashed,
                Role = "SuperAdmin",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine("[SEED] SuperAdmin created (username: superadmin)");
        }

        if (await db.Database.CanConnectAsync())
        {
            Console.WriteLine("[PATCH] Checking Products schema...");
            async Task Exec(string label, string sql)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(sql);
                    Console.WriteLine($"[PATCH] {label} OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PATCH] {label} FAIL: {ex.Message}");
                }
            }
            await Exec("Add CreatedAt", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','CreatedAt') IS NULL ALTER TABLE dbo.Products ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Products_CreatedAt_Runtime3 DEFAULT (SYSUTCDATETIME())");
            await Exec("Add UpdatedAt", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','UpdatedAt') IS NULL ALTER TABLE dbo.Products ADD UpdatedAt datetime2 NULL");
            await Exec("Add OwnerId", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','OwnerId') IS NULL ALTER TABLE dbo.Products ADD OwnerId int NULL");
            await Exec("Backfill OwnerId", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND EXISTS (SELECT 1 FROM Users) UPDATE p SET OwnerId = u.Id FROM Products p CROSS APPLY (SELECT TOP 1 Id FROM Users ORDER BY Id) u WHERE p.OwnerId IS NULL");
            await Exec("Index OwnerId", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_OwnerId' AND object_id = OBJECT_ID('dbo.Products')) CREATE INDEX IX_Products_OwnerId ON dbo.Products(OwnerId)");
            await Exec("FK OwnerId", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_Users_OwnerId') ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Users_OwnerId FOREIGN KEY(OwnerId) REFERENCES dbo.Users(Id) ON DELETE RESTRICT");
            await Exec("Enforce NOT NULL", "IF OBJECT_ID('dbo.Products','U') IS NOT NULL AND COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Products WHERE OwnerId IS NULL) BEGIN BEGIN TRY ALTER TABLE dbo.Products ALTER COLUMN OwnerId int NOT NULL; END TRY BEGIN CATCH PRINT 'OwnerId still nullable'; END CATCH END");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[STARTUP_ERROR] " + ex.Message);
        Console.WriteLine(ex);
    }
}

app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Inventory API running");
app.MapControllers();
app.Run();
