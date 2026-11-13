using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Claims;
using QL_DiemSV.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Add DbContext (SQL Server)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(60)));

// 2. Add Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "QLDiemSV.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 403;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            if (int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                && int.TryParse(principal.FindFirstValue("RoleID"), out var roleId)
                && await db.UserAccounts.AnyAsync(u => u.UserID == id && u.IsActive
                    && (u.RoleID ?? 0) == roleId
                    && (u.Role == null ? "SinhVien" : u.Role.RoleName) == principal.FindFirstValue(ClaimTypes.Role)))
                return;
            context.RejectPrincipal();
            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(
                context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("SignIn", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("TranscriptVerification", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddAuthorization(options =>
{
    foreach (var module in new[] { ContentAccess.Articles, ContentAccess.Curriculums })
    foreach (var action in new[] { "VIEW", "CREATE", "EDIT", "DELETE" })
        options.AddPolicy(module + ":" + action, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.Resource is HttpContext http
                ? ContentAccess.AllowsAsync(http.RequestServices.GetRequiredService<ApplicationDbContext>(), context.User, module, action)
                : Task.FromResult(false)));
});

// 3. Add ControllersWithViews + JSON PascalCase / camelCase compatibility
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<AcademicEmailNotifications>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<AcademicEmailNotifications>());
builder.Services.AddScoped<QL_DiemSV.Services.CourseCatalogCache>();

var app = builder.Build();

// 4. Auto-initialize and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);
        RoleSetup.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Đã xảy ra lỗi khi khởi tạo và seed CSDL.");
        if (args.Contains("--seed-only")) throw;
    }
}

if (args.Contains("--seed-only"))
{
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


