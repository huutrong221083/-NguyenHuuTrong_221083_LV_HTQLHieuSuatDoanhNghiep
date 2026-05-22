using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    var envName = builder.Environment.EnvironmentName;
    var fallbackConfig = new ConfigurationBuilder()
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{envName}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    connectionString =
        fallbackConfig.GetConnectionString("DefaultConnection")
        ?? fallbackConfig["ConnectionStrings:DefaultConnection"]
        ?? fallbackConfig["ConnectionStrings__DefaultConnection"];
}
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
}

var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
if (string.IsNullOrWhiteSpace(sqlBuilder.InitialCatalog))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' must include Database name.");
}

sqlBuilder.InitialCatalog = "LV2026";
connectionString = sqlBuilder.ConnectionString;

builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Smtp"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;

    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

builder.Services.AddAuthorization(options =>
{
    // Require authenticated users by default. Endpoints that must be public
    // need to be explicitly marked with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    foreach (var permission in Permissions.AllowedClaims)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAssertion(context =>
                context.User.IsInRole(Roles.Admin)
                || context.User.HasClaim(Permissions.PermissionClaimType, permission));
        });
    }

    options.AddPolicy("ManageUser", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin")
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.SettingsEdit));
    });

    options.AddPolicy("CanManageRoleClaims", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin")
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.SettingsEdit));
    });

    options.AddPolicy("KpiView", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole(Roles.Admin)
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.KpiView));
    });

    options.AddPolicy("KpiManage", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole(Roles.Admin)
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.KpiManage));
    });

    options.AddPolicy("DashboardView", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole(Roles.Admin)
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.KpiView)
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.MyKpiView)
            || context.User.IsInRole(Roles.Employee));
    });

    options.AddPolicy(Permissions.MyKpiView, policy =>
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole(Roles.Employee)
            || context.User.HasClaim(Permissions.PermissionClaimType, Permissions.MyKpiView));
    });
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "LV2026.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (IsApiRequest(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (IsApiRequest(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddScoped<IKpiService, KpiService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationRuntimeSettingsProvider, NotificationRuntimeSettingsProvider>();
builder.Services.AddScoped<ISecurityRuntimeSettingsProvider, SecurityRuntimeSettingsProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAiDataValidationService, AiDataValidationService>();
builder.Services.AddScoped<IAiRuntimeSettingsProvider, AiRuntimeSettingsProvider>();
builder.Services.AddScoped<IAiFeatureBuilderService, AiFeatureBuilderService>();
builder.Services.AddHttpClient<IAiPythonClient, AiPythonClient>();
builder.Services.AddScoped<ITaskDelayLinearRegressionService, TaskDelayLinearRegressionService>();
builder.Services.AddScoped<IEmployeePerformanceRandomForestService, EmployeePerformanceRandomForestService>();
builder.Services.AddScoped<IAiPredictionService, AiPredictionService>();
builder.Services.AddScoped<IAiEvaluationService, AiEvaluationService>();
builder.Services.AddHostedService<AiEvaluationHostedService>();
builder.Services.AddMemoryCache();

var app = builder.Build();

await EnsureIdentityRolesAsync(app.Services);
await EnsureIdentityRoleClaimsAsync(app.Services);
await EnsureTaskSchemaAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Lỗi server",
            });
        });
    });

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Portal}/{action=Dashboard}/{id?}");

app.Run();

static async Task EnsureIdentityRolesAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var defaultRoles = new[] { "Admin", "Manager", "Employee" };
    foreach (var roleName in defaultRoles)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            continue;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Không thể khởi tạo role mặc định '{roleName}': {string.Join(" | ", result.Errors.Select(x => x.Description))}");
        }
    }
}

static async Task EnsureIdentityRoleClaimsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var roleEntry in Permissions.DefaultRoleClaims)
    {
        var roleName = roleEntry.Key;
        var desiredClaims = roleEntry.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            throw new InvalidOperationException($"Không tìm thấy role mặc định '{roleName}'.");
        }

        var currentClaims = await roleManager.GetClaimsAsync(role);
        var currentPermissionClaims = currentClaims
            .Where(x => string.Equals(x.Type, Permissions.PermissionClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .ToList();

        foreach (var claimValue in currentPermissionClaims)
        {
            if (!desiredClaims.Contains(claimValue, StringComparer.OrdinalIgnoreCase))
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, new Claim(Permissions.PermissionClaimType, claimValue));
                if (!removeResult.Succeeded)
                {
                    throw new InvalidOperationException($"Không thể gỡ permission '{claimValue}' khỏi role '{roleName}': {string.Join(" | ", removeResult.Errors.Select(x => x.Description))}");
                }
            }
        }

        foreach (var claimValue in desiredClaims)
        {
            if (currentPermissionClaims.Any(x => string.Equals(x, claimValue, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var addResult = await roleManager.AddClaimAsync(role, new Claim(Permissions.PermissionClaimType, claimValue));
            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException($"Không thể gán permission '{claimValue}' cho role '{roleName}': {string.Join(" | ", addResult.Errors.Select(x => x.Description))}");
            }
        }
    }
}

static async Task EnsureTaskSchemaAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await EnsureColumnAsync(dbContext, "CONGVIEC", "NGAYBATDAU", "datetime2(0)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "PHANTRAMHOANTHANH", "decimal(5,2)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "NGAYTAO", "datetime2(0)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "NGUOITAO", "nvarchar(128)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "NGAYCAPNHAT", "datetime2(0)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "NGUOICAPNHAT", "nvarchar(128)");
    await EnsureColumnAsync(dbContext, "CONGVIEC", "DAXOA", "bit");

    await EnsureColumnAsync(dbContext, "NHATKYCONGVIEC", "PHANTRAMHOANTHANH", "decimal(5,2)");
    await EnsureColumnAsync(dbContext, "PHANCONGNHANVIEN", "PHANTRAMHOANTHANH", "decimal(5,2)");
    await EnsureColumnAsync(dbContext, "TIENDOCONGVIEC", "PHANTRAMHOANTHANH", "decimal(5,2)");
}

static async Task EnsureColumnAsync(AppDbContext dbContext, string tableName, string columnName, string columnType)
{
    var sql = $"""
IF COL_LENGTH('dbo.{tableName}', '{columnName}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{tableName}] ADD [{columnName}] {columnType} NULL;
END
""";

    await dbContext.Database.ExecuteSqlRawAsync(sql);
}

static bool IsApiRequest(PathString path)
{
    if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    var apiPrefixes = new[]
    {
        "/account-management",
        "/ai",
        "/chucvu",
        "/dashboard",
        "/duan",
        "/kpi",
        "/kynang",
        "/nhanvien",
        "/nhatkyhoatdong",
        "/nhom",
        "/phongban",
        "/system",
        "/thongbao"
    };

    return apiPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
