using System.ComponentModel.DataAnnotations;

namespace LuanVan.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tài khoản hoặc email.")]
    [Display(Name = "Tài khoản hoặc Email")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}




