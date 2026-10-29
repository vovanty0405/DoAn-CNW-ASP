using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("PhanQuyen")]
public class PhanQuyen
{
    public int RoleID { get; set; }

    [MaxLength(50)]
    public string MaChucNang { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MaHanhDong { get; set; } = string.Empty;

    [ForeignKey("RoleID")]
    public virtual Role? Role { get; set; }

    [ForeignKey("MaChucNang")]
    public virtual ChucNang? ChucNang { get; set; }

    [ForeignKey("MaHanhDong")]
    public virtual HanhDong? HanhDong { get; set; }
}
