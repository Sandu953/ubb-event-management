using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UbbEvents.Api.Data;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Infrastructure;
using UbbEvents.Api.Models;
using UbbEvents.Api.Repositories;
using UbbEvents.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection using user secrets or environment variables.")));
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(JwtOptions.IsValid, "Configure JWT issuer, audience, a signing key of at least 32 UTF-8 bytes, and expiration of 5-1440 minutes.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "sub", RoleClaimType = "role"
        };
        options.IncludeErrorDetails = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
                    context.Fail("Invalid token subject.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options => options.AddPolicy("GlobalAdmin", policy =>
    policy.RequireAuthenticatedUser().RequireRole(nameof(GlobalRole.ADMIN))));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<OrganizationPermissions>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<EventLockRepository>();
builder.Services.AddSingleton<EventLifecycle>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<WaitlistService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AttendanceService>();
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await Results.Problem(statusCode: 429, title: "Too many authentication requests.").ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "UBB Events API", Version = "v1" });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "UbbEvents.Api.xml"));
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Use the accessToken returned by POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.OperationFilter<AuthorizationOperationFilter>();
});
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
        title: Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(context.HttpContext.Response.StatusCode))
        .ExecuteAsync(context.HttpContext);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => TypedResults.Ok(new HealthResponse("ok"))).AllowAnonymous();
// Schema changes are an explicit deployment step. Startup never applies migrations.
await DevelopmentAdminSeeder.SeedAsync(app);
app.Run();

public partial class Program;
