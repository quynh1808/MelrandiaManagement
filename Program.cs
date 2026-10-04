using System.Security.Claims;
using System.Globalization;
using System.Threading.RateLimiting;
using MelrandiaManagement.Components;
using MelrandiaManagement.Configuration;
using MelrandiaManagement.Data;
using MelrandiaManagement.Infrastructure;
using MelrandiaManagement.Models;
using MelrandiaManagement.Runtime;
using MelrandiaManagement.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

const string resetAdminPasswordCommand = "reset-admin-password";
var resetAdminPasswordMode = args.Contains(resetAdminPasswordCommand, StringComparer.OrdinalIgnoreCase);
var hostArguments = args.Where(argument =>
    !argument.Equals(resetAdminPasswordCommand, StringComparison.OrdinalIgnoreCase)).ToArray();
var builder = WebApplication.CreateBuilder(hostArguments);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddOptions<PostgreSqlOptions>()
    .Bind(builder.Configuration.GetSection(PostgreSqlOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<BootstrapAdminOptions>()
    .Bind(builder.Configuration.GetSection(BootstrapAdminOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<InitialProjectOptions>()
    .Bind(builder.Configuration.GetSection(InitialProjectOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<PortalPerformanceOptions>()
    .Bind(builder.Configuration.GetSection(PortalPerformanceOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<ArticleMediaOptions>()
    .Bind(builder.Configuration.GetSection(ArticleMediaOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PostgreSqlOptions>, PostgreSqlOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<BootstrapAdminOptions>, BootstrapAdminOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<InitialProjectOptions>, InitialProjectOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PortalPerformanceOptions>, PortalPerformanceOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ArticleMediaOptions>, ArticleMediaOptionsValidator>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 210 * 1024 * 1024L);

var databaseSettings = builder.Configuration.GetSection(PostgreSqlOptions.SectionName)
    .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();
builder.Services.AddPooledDbContextFactory<ManagementDbContext>(options =>
    options.UseNpgsql(ManagementDbConnection.Build(databaseSettings), npgsql =>
        npgsql.CommandTimeout(databaseSettings.CommandTimeoutSeconds)));

builder.Services.AddIdentity<PortalUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ManagementDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.Name = "MelrandiaManagement.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagePortal", policy =>
        policy.RequireAuthenticatedUser().RequireRole(PortalRoles.Administrator));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("MelrandiaManagement");

builder.Services.AddScoped<PortalDatabaseInitializer>();
builder.Services.AddScoped<PortalAdministrationService>();
builder.Services.AddScoped<ArticleAdministrationService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PublicProjectCatalog>();
builder.Services.AddSingleton<PublicArticleCatalog>();
builder.Services.AddSingleton<ArticleMarkdownRenderer>();
builder.Services.AddSingleton<ArticleMediaStorage>();
builder.Services.AddHttpClient("project-health", (serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<InitialProjectOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(settings.HealthCheckTimeoutSeconds);
});
builder.Services.AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"]);
builder.Services.AddHostedService<PortalBootstrapHostedService>();
builder.Services.AddHostedService<ProjectHealthMonitor>();

var app = builder.Build();

var articleMediaStorage = app.Services.GetRequiredService<ArticleMediaStorage>();
Directory.CreateDirectory(articleMediaStorage.RootPath);

if (resetAdminPasswordMode)
{
    var username = app.Configuration["AdminRecovery:Username"] ?? "admin";
    var password = app.Configuration["AdminRecovery:Password"];
    if (string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException(
            "Hãy đặt AdminRecovery__Password hoặc User Secret AdminRecovery:Password trước khi recovery.");

    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
    await database.Database.MigrateAsync();
    var administration = scope.ServiceProvider.GetRequiredService<PortalAdministrationService>();
    await administration.ResetAdministratorPasswordAsync(username, password);
    app.Logger.LogWarning("Đã recovery, mở khóa Administrator {Username}; hãy xóa recovery secret.", username);
    return;
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(articleMediaStorage.RootPath),
    RequestPath = "/media",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        context.Context.Response.Headers.XContentTypeOptions = "nosniff";
    }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{ Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.MapPost("/auth/login", async (HttpContext http, IAntiforgery antiforgery,
    SignInManager<PortalUser> signInManager, UserManager<PortalUser> userManager,
    PortalAdministrationService administration, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = SafeLocalReturnUrl(form["returnUrl"].ToString(), "/Management");
    var user = await userManager.FindByNameAsync(username);
    if (user is null || !user.IsEnabled)
        return Results.Redirect($"/login?error=invalid&username={Uri.EscapeDataString(username)}&returnUrl={Uri.EscapeDataString(returnUrl)}");

    var result = await signInManager.PasswordSignInAsync(user, password, false, lockoutOnFailure: true);
    if (result.Succeeded)
    {
        await administration.RecordLoginAsync(user, cancellationToken);
        return Results.Redirect(returnUrl);
    }
    var error = result.IsLockedOut ? "locked" : "invalid";
    return Results.Redirect($"/login?error={error}&username={Uri.EscapeDataString(username)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).AllowAnonymous().RequireRateLimiting("login");

app.MapPost("/auth/logout", async (HttpContext http, IAntiforgery antiforgery,
    SignInManager<PortalUser> signInManager, PortalAdministrationService administration,
    CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var actor = http.User.Identity?.Name ?? "unknown";
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    await signInManager.SignOutAsync();
    await administration.RecordLogoutAsync(actor, userId, cancellationToken);
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapPost("/management/projects/save", async (HttpContext http, IAntiforgery antiforgery,
    PortalAdministrationService administration, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    try
    {
        var id = Guid.TryParse(form["id"], out var parsedId) ? parsedId : (Guid?)null;
        var request = new SaveProjectRequest(id, form["projectKey"]!, form["slug"]!,
            form["shortName"]!, form["displayName"]!, form["description"]!,
            form["publicUrl"]!, form["healthUrl"]!, form["lifecycleStatus"]!,
            form.ContainsKey("isEnabled"), form.ContainsKey("isPublic"),
            form.ContainsKey("isFeatured"), int.TryParse(form["displayOrder"], out var order) ? order : 0);
        await administration.SaveProjectAsync(request, http.User.Identity?.Name ?? "unknown",
            cancellationToken);
        return Results.Redirect("/Management/Projects?status=saved");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Redirect($"/Management/Projects?error={Uri.EscapeDataString(exception.Message)}");
    }
    catch (DbUpdateException)
    {
        var message = Uri.EscapeDataString("ProjectKey hoặc Slug đã tồn tại.");
        return Results.Redirect($"/Management/Projects?error={message}");
    }
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/articles/save", async (HttpContext http, IAntiforgery antiforgery,
    ArticleAdministrationService articles, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId) ||
        !Guid.TryParse(form["categoryId"], out var categoryId))
        return Results.Redirect("/Management/Articles?error=Du-lieu-khong-hop-le");
    try
    {
        var id = Guid.TryParse(form["id"], out var parsedId) ? parsedId : (Guid?)null;
        var request = new SaveArticleRequest(id, categoryId, form["slug"]!, form["title"]!,
            form["summary"]!, form["contentMarkdown"]!, form["status"]!,
            ParseUtcDateTime(form["publishedAtUtc"]!), form["coverAlt"]!,
            int.TryParse(form["version"], out var version) ? version : 1);
        var articleId = await articles.SaveAsync(request, form.Files.GetFiles("cover"),
            form.Files.GetFiles("gallery"), authorId, http.User.Identity?.Name ?? "unknown",
            cancellationToken);
        return Results.Redirect($"/Management/Articles?status=saved&id={articleId}");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Redirect($"/Management/Articles?error={Uri.EscapeDataString(exception.Message)}");
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Redirect("/Management/Articles?error=Bai-viet-da-duoc-chinh-sua-o-phien-khac-hay-tai-lai-trang");
    }
    catch (DbUpdateException)
    {
        return Results.Redirect("/Management/Articles?error=Slug-bai-viet-da-ton-tai");
    }
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/articles/duplicate", async (HttpContext http, IAntiforgery antiforgery,
    ArticleAdministrationService articles, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    if (!Guid.TryParse(form["articleId"], out var articleId) ||
        !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId))
        return Results.Redirect("/Management/Articles?error=Du-lieu-khong-hop-le");
    try
    {
        var duplicateId = await articles.DuplicateAsync(articleId, authorId,
            http.User.Identity?.Name ?? "unknown", cancellationToken);
        return Results.Redirect($"/Management/Articles?status=duplicated&id={duplicateId}");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Redirect($"/Management/Articles?error={Uri.EscapeDataString(exception.Message)}");
    }
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/articles/delete", async (HttpContext http, IAntiforgery antiforgery,
    ArticleAdministrationService articles, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    if (!Guid.TryParse(form["articleId"], out var articleId))
        return Results.Redirect("/Management/Articles?error=Du-lieu-khong-hop-le");
    try
    {
        await articles.DeleteAsync(articleId, http.User.Identity?.Name ?? "unknown", cancellationToken);
        return Results.Redirect("/Management/Articles?status=deleted");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Redirect($"/Management/Articles?error={Uri.EscapeDataString(exception.Message)}");
    }
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/users/create", async (HttpContext http, IAntiforgery antiforgery,
    PortalAdministrationService administration, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    var result = await administration.CreateUserAsync(form["username"]!, form["displayName"]!,
        form["password"]!, form.ContainsKey("administrator"),
        http.User.Identity?.Name ?? "unknown", cancellationToken);
    return result.Succeeded
        ? Results.Redirect("/Management/Users?status=created")
        : Results.Redirect($"/Management/Users?error={Uri.EscapeDataString(string.Join("; ", result.Errors.Select(x => x.Description)))}");
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/access/grant", async (HttpContext http, IAntiforgery antiforgery,
    PortalAdministrationService administration, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    if (!Guid.TryParse(form["userId"], out var userId) ||
        !Guid.TryParse(form["projectId"], out var projectId) ||
        !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        return Results.Redirect("/Management/Access?error=Du-lieu-khong-hop-le");
    try
    {
        await administration.GrantAccessAsync(userId, projectId, form["accessRole"]!, actorId,
            http.User.Identity?.Name ?? "unknown", cancellationToken);
        return Results.Redirect("/Management/Access?status=granted");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Redirect($"/Management/Access?error={Uri.EscapeDataString(exception.Message)}");
    }
}).RequireAuthorization("ManagePortal");

app.MapPost("/management/access/revoke", async (HttpContext http, IAntiforgery antiforgery,
    PortalAdministrationService administration, CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(http);
    var form = await http.Request.ReadFormAsync(cancellationToken);
    if (Guid.TryParse(form["userId"], out var userId) && Guid.TryParse(form["projectId"], out var projectId))
        await administration.RevokeAccessAsync(userId, projectId,
            http.User.Identity?.Name ?? "unknown", cancellationToken);
    return Results.Redirect("/Management/Access?status=revoked");
}).RequireAuthorization("ManagePortal");

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static string SafeLocalReturnUrl(string value, string fallback) =>
    string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') ||
    value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/\\", StringComparison.Ordinal)
        ? fallback
        : value;

static DateTime? ParseUtcDateTime(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    return DateTime.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
        ? parsed : null;
}
