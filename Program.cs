using DotNetEnv;
using kuv_api.Data;
using kuv_api.Models;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

Env.Load();
var builder = WebApplication.CreateBuilder(args);

// ==========================================
//             Build configuration
// ==========================================

///
/// Controllers
/// 
builder.Services.AddControllers();

///
/// Database
/// 
var connectionString = DatabaseConnection.GetConnectionString(builder.Configuration);
builder.Services.AddDbContext<KuvDbContext>(options => options.UseNpgsql(connectionString));

///
/// Auth configuration
/// 

// Ensure JWT key is configured
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) || Encoding.UTF8.GetByteCount(builder.Configuration["Jwt:Key"]!) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes using an environment variable.");
}

// Configure authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey    = true,
            IssuerSigningKey            = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer              = true,
            ValidIssuer                 = builder.Configuration["Jwt:Issuer"],
            ValidateAudience            = true,
            ValidAudience               = builder.Configuration["Jwt:Audience"],
            ValidateLifetime            = true,
            ClockSkew                   = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ==========================================
//                  Services
// ==========================================

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddOpenApi();

var app = builder.Build();

// ==========================================
//      HTTP pipeline configuration
// ==========================================

///
/// Scalar API (Open API)
/// 
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

///
/// HTTPS redirection
///  .
app.UseHttpsRedirection();

///
/// Authentication and authorization
/// 
app.UseAuthentication();
app.UseAuthorization();

///
/// Controllers
/// 
app.MapControllers();

app.Run();
