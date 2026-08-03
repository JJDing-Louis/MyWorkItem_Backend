using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Api.Security;
using MyWorkItem.Domain.Constants;
using MyWorkItem.Infrastructure;
using MyWorkItem.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection 不可為空。");
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey 至少需要 32 bytes。");
}

builder.Services.AddMyWorkItemInfrastructure(connectionString, jwtOptions);
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "mwi_antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[CookieNames.AccessToken];
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var function in FunctionCodes.All)
    {
        options.AddPolicy(function, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new FunctionRequirement(function)));
    }
});
builder.Services.AddScoped<IAuthorizationHandler, FunctionAuthorizationHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MyWorkItem Backend API", Version = "v1" });
    options.AddSecurityDefinition("CookieAuthentication", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = CookieNames.AccessToken,
        Description = "登入後由瀏覽器自動攜帶的 HttpOnly Access Token Cookie。"
    });
    options.OperationFilter<SecurityOperationFilter>();
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CsrfValidationMiddleware>();
app.UseAuthorization();

var swaggerEnabled = builder.Configuration.GetValue<bool?>("Swagger:Enabled")
    ?? app.Environment.IsDevelopment();
if (!app.Environment.IsProduction() && swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyWorkItem Backend API v1");
        options.InjectJavascript("/swagger-ui/csrf.js");
        options.UseRequestInterceptor("function(request) { request.credentials = 'same-origin'; const unsafeMethods = ['POST', 'PUT', 'PATCH', 'DELETE']; if (unsafeMethods.includes((request.method || 'GET').toUpperCase())) { const prefix = 'XSRF-TOKEN='; const cookie = document.cookie.split('; ').find(item => item.startsWith(prefix)); request.headers['X-CSRF-TOKEN'] = decodeURIComponent(cookie ? cookie.substring(prefix.length) : ''); } return request; }");
        options.UseResponseInterceptor("function(response) { if (response.status === 200 && response.url && response.url.includes('/api/v1/auth/login')) { return fetch('/api/v1/auth/csrf', { credentials: 'same-origin' }).then(() => response); } return response; }");
    });
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.Run();

public partial class Program;
