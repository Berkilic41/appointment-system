using AppointmentSystem.Bll.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using AppointmentSystem.Bll.Services.Interfaces;
using AppointmentSystem.Data;
using AppointmentSystem.Data.Repositories;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllersWithViews();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10; opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = 429;
});

// Data layer
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();

// Service layer
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProviderService, ProviderService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

var app = builder.Build();

app.UseMiddleware<AppointmentSystem.Web.Middleware.CorrelationIdMiddleware>();
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]        = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:";
    await next();
});
app.UseRateLimiter();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapGet("/health", async (DbConnectionFactory db) =>
{
    try { using var c = db.CreateConnection(); await c.OpenAsync(); return Results.Ok(new { status = "healthy", database = "ok", timestamp = DateTime.UtcNow }); }
    catch { return Results.Json(new { status = "degraded", database = "error", timestamp = DateTime.UtcNow }, statusCode: 503); }
}).AllowAnonymous();

    Log.Information("AppointmentSystem starting up");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "AppointmentSystem terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }
