using DotNetEnv;
using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Entities;
using kuv_api.Services;
using System.Net.Http.Headers;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

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

// Configure request limits for video/creation uploads (up to 100 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

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

        // Browsers cannot set headers on a WebSocket or media elements (videos, thumbnails) easily, so tokens can come in the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if ((context.Request.Path.StartsWithSegments("/ws/client") || context.Request.Path.Value?.Contains("/video") == true || context.Request.Path.Value?.EndsWith("/thumbnail") == true)
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
builder.Services.AddScoped<GoogleAuthService>();
// Where the files are kept. Remote (the default) is the file server of the VPS, so every API, wherever it runs, sees
// the same files; Disk keeps them in a folder of this machine (the server itself, or a laptop with no connection).
var filesMode = builder.Configuration["Files:Mode"] ?? "Remote";

if ("Disk" == filesMode)
{
    var filesRoot = builder.Configuration["Files:Root"] ?? Path.Combine(builder.Environment.ContentRootPath, "uploads");

    builder.Services.AddSingleton<IFileStore>(new DiskFileStore(filesRoot));
}

else if ("Remote" == filesMode)
{
    var remoteUrl = builder.Configuration["Files:RemoteUrl"]
        ?? throw new InvalidOperationException("Files:RemoteUrl is not set.");
    var remoteKey = builder.Configuration["Files:RemoteKey"]
        ?? throw new InvalidOperationException("Files:RemoteKey is not set. Set it, or set Files:Mode to Disk to keep the files on this machine.");
    var login = Convert.ToBase64String(Encoding.UTF8.GetBytes($"kuv:{remoteKey}"));

    builder.Services.AddHttpClient<IFileStore, RemoteFileStore>(client =>
    {
        client.BaseAddress = new Uri(remoteUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", login);

        // Videos are big: only the wait for an answer is limited, not the whole transfer
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}

else
{
    throw new InvalidOperationException($"Files:Mode must be Remote or Disk, not '{filesMode}'.");
}

builder.Services.AddSingleton<ProfilePhotoService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddSingleton<CreationStorage>();
builder.Services.AddSingleton<ProgramFileValidator>();
builder.Services.AddScoped<CreationService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<FeedService>();
builder.Services.AddSingleton<ICubeDirectory, ConfigCubeDirectory>();
builder.Services.AddSingleton<CubeHub>();
builder.Services.AddOpenApi();

var app = builder.Build();

// ==========================================
//      HTTP pipeline configuration
// ==========================================

///
/// CORS (must run before ExceptionHandler and Authentication so preflight and error responses include CORS headers)
///
app.UseCors();

app.UseExceptionHandler();

///
/// Scalar API (Open API)
/// 
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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
