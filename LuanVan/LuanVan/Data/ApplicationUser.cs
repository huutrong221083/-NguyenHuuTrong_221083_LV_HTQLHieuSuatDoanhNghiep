using Microsoft.AspNetCore.Identity;
using LuanVan.Models;

namespace LuanVan.Data
{
    public class ApplicationUser : IdentityUser
    {
        public NhanVien? NhanVien { get; set; }
    }
}
