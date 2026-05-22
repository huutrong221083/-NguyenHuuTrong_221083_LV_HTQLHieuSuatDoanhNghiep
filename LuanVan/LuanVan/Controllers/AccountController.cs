using System.Text;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers;

public class AccountController : Controller
{
    private const int EmployeeStatusInactive = 0;

    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly SignInManager<Data.ApplicationUser> _signInManager;
    private readonly UserManager<Data.ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IEmailService _emailService;
    private readonly ISecurityRuntimeSettingsProvider _securityRuntimeSettingsProvider;
    private readonly ILogger<AccountController> _logger;

    private static bool LooksLikeBcryptHash(string? hash)
    {
        return !string.IsNullOrWhiteSpace(hash)
            && (hash.StartsWith("$2a$", StringComparison.Ordinal)
                || hash.StartsWith("$2b$", StringComparison.Ordinal)
                || hash.StartsWith("$2y$", StringComparison.Ordinal));
    }

    private async Task<bool> TryLegacyBcryptSignInAndMigrateAsync(ApplicationUser user, string password, bool rememberMe)
    {
        if (!LooksLikeBcryptHash(user.PasswordHash))
        {
            return false;
        }

        bool isValidLegacyPassword;
        try
        {
            isValidLegacyPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể verify BCrypt hash cho user {UserId}", user.Id);
            return false;
        }

        if (!isValidLegacyPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return false;
        }

        var identityHash = _userManager.PasswordHasher.HashPassword(user, password);
        user.PasswordHash = identityHash;
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogWarning("Migrate hash BCrypt -> Identity thất bại cho user {UserId}: {Errors}", user.Id, string.Join(" | ", updateResult.Errors.Select(x => x.Description)));
            return false;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _signInManager.SignInAsync(user, rememberMe);
        return true;
    }

    private void SetRoleContextForLayout()
    {
        var roleKey = User.IsInRole("Admin")
            ? "admin"
            : User.IsInRole("Manager")
                ? "manager"
                : "employee";

        var role = User.IsInRole("Admin")
            ? "Admin"
            : User.IsInRole("Manager")
                ? "Manager"
                : "Employee";

        ViewData["RoleKey"] = roleKey;
        ViewData["Role"] = role;
    }

    public AccountController(
        AppDbContext dbContext,
        IConfiguration configuration,
        SignInManager<Data.ApplicationUser> signInManager,
        UserManager<Data.ApplicationUser> userManager,
        IAuditLogService auditLogService,
        IEmailService emailService,
        ISecurityRuntimeSettingsProvider securityRuntimeSettingsProvider,
        ILogger<AccountController> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _emailService = emailService;
        _securityRuntimeSettingsProvider = securityRuntimeSettingsProvider;
        _logger = logger;
    }

    private async Task CreateAdminNotificationAsync(string loai, string noiDung)
    {
        try
        {
            var adminIds = await (
                from nv in _dbContext.NhanViens.AsNoTracking()
                join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
                join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where r.Name == "Admin"
                select nv.MaNhanVien
            ).Distinct().ToListAsync();

            if (!adminIds.Any())
            {
                return;
            }

            var loaiRow = await _dbContext.LoaiThongBaos.FirstOrDefaultAsync(x => x.TenLoai == loai);
            if (loaiRow == null)
            {
                var nextId = (await _dbContext.LoaiThongBaos.MaxAsync(x => (int?)x.MaLoai) ?? 0) + 1;
                loaiRow = new LoaiThongBao { MaLoai = nextId, TenLoai = loai };
                _dbContext.LoaiThongBaos.Add(loaiRow);
                await _dbContext.SaveChangesAsync();
            }

            var tb = new ThongBao
            {
                MaLoai = loaiRow.MaLoai,
                NoiDung = noiDung,
                ThoiGian = DateTime.Now
            };
            _dbContext.ThongBaos.Add(tb);
            await _dbContext.SaveChangesAsync();

            _dbContext.ThongBaoNhanViens.AddRange(adminIds.Select(id => new ThongBaoNhanVien
            {
                MaThongBao = tb.MaThongBao,
                MaNhanVien = id,
                DaDoc = false
            }));
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // Không chặn luồng đăng nhập nếu thông báo lỗi.
        }
    }

    private void EnsureDbConnectionStringInitialized()
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(conn.ConnectionString))
        {
            return;
        }

        var fallback =
            _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(fallback))
        {
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(fallback);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "LV2026";
            }

            conn.ConnectionString = builder.ConnectionString;
        }
        catch
        {
            conn.ConnectionString = fallback;
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Portal");
        }

        ViewData["Title"] = "Đăng nhập";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["Title"] = "Đăng nhập";
        EnsureDbConnectionStringInitialized();
        var securitySettings = await _securityRuntimeSettingsProvider.GetAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userInput = model.UserNameOrEmail.Trim();
        var user = userInput.Contains('@')
            ? await _userManager.FindByEmailAsync(userInput)
            : await _userManager.FindByNameAsync(userInput);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
            return View(model);
        }

        var employeeStatus = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == user.Id)
            .Select(x => x.TrangThai)
            .FirstOrDefaultAsync();

        if (employeeStatus.HasValue && employeeStatus.Value == EmployeeStatusInactive)
        {
            await _auditLogService.LogByUserIdAsync(user.Id, "Đăng nhập thất bại: tài khoản đang bị vô hiệu hóa");
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        Microsoft.AspNetCore.Identity.SignInResult signIn;
        try
        {
            if (securitySettings.AutoLock)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    await _auditLogService.LogByUserIdAsync(user.Id, "Đăng nhập thất bại: tài khoản bị khóa");
                    ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa tạm thời. Vui lòng liên hệ với Quản lý để được mở khóa tài khoản.");
                    return View(model);
                }

                signIn = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                if (signIn.Succeeded)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                    await _signInManager.SignInAsync(user, model.RememberMe);
                }
                else if (!signIn.IsLockedOut)
                {
                    var failed = await _userManager.AccessFailedAsync(user);
                    if (failed.Succeeded)
                    {
                        var refreshed = await _userManager.FindByIdAsync(user.Id);
                        var failedCount = refreshed?.AccessFailedCount ?? 0;
                        if (failedCount >= securitySettings.MaxFailed)
                        {
                            var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                            await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
                            await CreateAdminNotificationAsync(
                                "ACCOUNT_LOCK",
                                $"Tài khoản {(user.UserName ?? user.Id)} đã bị khóa tự động do đăng nhập sai quá {securitySettings.MaxFailed} lần.");
                            signIn = Microsoft.AspNetCore.Identity.SignInResult.LockedOut;
                        }
                    }
                }
            }
            else
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    await _userManager.ResetAccessFailedCountAsync(user);
                }

                signIn = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                if (signIn.Succeeded)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                    await _signInManager.SignInAsync(user, model.RememberMe);
                }
            }
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "PasswordHash không đúng định dạng Identity cho user {UserId}", user.Id);

            var migratedSignIn = await TryLegacyBcryptSignInAndMigrateAsync(user, model.Password, model.RememberMe);
            if (migratedSignIn)
            {
                await _auditLogService.LogByUserIdAsync(user.Id, "Đăng nhập thành công (migrate hash BCrypt -> Identity)");

                if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Dashboard", "Portal");
            }

            ModelState.AddModelError(string.Empty, "Tài khoản chưa sẵn sàng đăng nhập do dữ liệu mật khẩu cũ. Vui lòng dùng Quên mật khẩu để đặt lại.");
            return View(model);
        }

        if (!signIn.Succeeded)
        {
            var failedReason = signIn.IsLockedOut
                ? "Đăng nhập thất bại: tài khoản bị khóa"
                : "Đăng nhập thất bại: sai mật khẩu";
            await _auditLogService.LogByUserIdAsync(user.Id, failedReason);

            if (signIn.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa tạm thời. Vui lòng liên hệ với Quản lý để được mở khóa tài khoản.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
            }

            return View(model);
        }

        await _auditLogService.LogByUserIdAsync(user.Id, "Đăng nhập thành công");

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Dashboard", "Portal");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Portal");
        }

        ViewData["Title"] = "Quên mật khẩu";
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["Title"] = "Quên mật khẩu";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email không tồn tại trong hệ thống.");
            return View(model);
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { token = encodedToken, email = user.Email },
                Request.Scheme);

            if (string.IsNullOrWhiteSpace(resetUrl))
            {
                throw new InvalidOperationException("Không thể tạo đường dẫn đặt lại mật khẩu.");
            }

            var recipientName = string.IsNullOrWhiteSpace(user.UserName) ? user.Email ?? string.Empty : user.UserName;
            await _emailService.SendPasswordResetEmailAsync(user.Email!, recipientName, resetUrl);
            model.IsSubmitted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email đặt lại mật khẩu thất bại cho {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Không thể gửi email đặt lại mật khẩu lúc này. Vui lòng thử lại sau.");
            return View(model);
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        ViewData["Title"] = "Đặt lại mật khẩu";

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
        {
            TempData["AuthError"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel
        {
            Token = token,
            Email = email
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["Title"] = "Đặt lại mật khẩu";
        var securitySettings = await _securityRuntimeSettingsProvider.GetAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if ((model.NewPassword?.Length ?? 0) < securitySettings.MinLength)
        {
            ModelState.AddModelError(nameof(model.NewPassword), $"Mật khẩu mới phải có ít nhất {securitySettings.MinLength} ký tự.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email không tồn tại trong hệ thống.");
            return View(model);
        }

        string decodedToken;
        try
        {
            var tokenBytes = WebEncoders.Base64UrlDecode(model.Token);
            decodedToken = Encoding.UTF8.GetString(tokenBytes);
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
            return View(model);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _auditLogService.LogByUserIdAsync(user.Id, "Đặt lại mật khẩu thành công");
        model.IsCompleted = true;

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Đổi mật khẩu";
        SetRoleContextForLayout();
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        ViewData["Title"] = "Đổi mật khẩu";
        SetRoleContextForLayout();
        var securitySettings = await _securityRuntimeSettingsProvider.GetAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if ((model.NewPassword?.Length ?? 0) < securitySettings.MinLength)
        {
            ModelState.AddModelError(nameof(model.NewPassword), $"Mật khẩu mới phải có ít nhất {securitySettings.MinLength} ký tự.");
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!changeResult.Succeeded)
        {
            foreach (var error in changeResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _auditLogService.LogByUserIdAsync(user.Id, "Đổi mật khẩu thành công");

        TempData["AuthSuccess"] = "Mật khẩu đã được cập nhật thành công.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Không có quyền truy cập";
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var currentUserId = _userManager.GetUserId(User);
        try
        {
            await _auditLogService.LogByUserIdAsync(currentUserId, "Đăng xuất");
        }
        catch
        {
            // Không chặn luồng đăng xuất nếu audit log lỗi.
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}



