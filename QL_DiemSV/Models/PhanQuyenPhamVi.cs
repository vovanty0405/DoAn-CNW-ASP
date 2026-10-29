using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("PhanQuyenPhamVi")]
public class PhanQuyenPhamVi
{
    public int RoleID { get; set; }

    [MaxLength(50)]
    public string MaPhamVi { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Rule { get; set; }

    public bool IsActive { get; set; } = false;

    [ForeignKey("RoleID")]
    public virtual Role? Role { get; set; }
}
