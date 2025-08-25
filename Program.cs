using InventoryApi.Data;
using InventoryApi.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer; 
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)),
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

// Enhanced migration application & logging + SuperAdmin seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            Console.WriteLine("[MIGRATIONS] Applying pending migrations: " + string.Join(", ", pending));
            await db.Database.MigrateAsync();
            Console.WriteLine("[MIGRATIONS] Applied successfully.");
        }
        else
        {
            Console.WriteLine("[MIGRATIONS] No pending migrations.");
        }

        // Seed SuperAdmin user if none exists
        if (!await db.Users.AnyAsync(u => u.Role == "SuperAdmin"))
        {
            var superPassword = "SuperAdmin#12345"; // In real production load from secure secret
            var hashed = BCrypt.Net.BCrypt.HashPassword(superPassword);
            db.Users.Add(new User
            {
                Username = "superadmin",
                PasswordHash = hashed,
                Role = "SuperAdmin",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Console.WriteLine("[SEED] SuperAdmin user created (username: superadmin). CHANGE THE PASSWORD IMMEDIATELY.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[MIGRATION_ERROR] " + ex.Message);
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
