using DotNetEnv;
using kuv_api.Data;
using kuv_api.Exceptions;
using kuv_api.Models;
using kuv_api.Services;
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
builder.Services.AddExceptionHandler<KuvExceptionHandler>();
builder.Services.AddProblemDetails();

// Origins allowed to call the API from a browser (see "Cors:AllowedOrigins" in appsettings).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
));

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

        // Browsers cannot set headers on a WebSocket or media elements easily, so tokens can come in the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if ((context.Request.Path.StartsWithSegments("/ws/client") || context.Request.Path.Value?.Contains("/video") == true)
                    && (context.Request.Query.TryGetValue("access_token", out var token) || context.Request.Query.TryGetValue("token", out token)))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ==========================================
//                  Services
// ==========================================

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<CreationService>();
builder.Services.AddSingleton<ICubeDirectory, ConfigCubeDirectory>();
builder.Services.AddSingleton<CubeHub>();
builder.Services.AddOpenApi();

var app = builder.Build();

// ==========================================
//      HTTP pipeline configuration
// ==========================================

app.UseExceptionHandler();

///
/// Scalar API (Open API)
/// 
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

///
/// CORS (must run before authentication so preflight requests are answered)
///
app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

///
/// Authentication and authorization
/// 
app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();

///
/// Controllers
/// 
app.MapControllers();

app.Run();
