using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("KetQuaHocTap")]
public class KetQuaHocTap
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaKQ { get; set; }

    [MaxLength(20)]
    public string? MaSV { get; set; }

    [MaxLength(20)]
    public string? MaLHP { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal? HocPhiDangKy { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MienGiam { get; set; }
    public DateTime? NgayDangKy { get; set; }

    public double? DiemCC { get; set; }

    public double? DiemGK { get; set; }

    public double? DiemCK { get; set; }

    [MaxLength(5)]
    public string? DiemChu { get; set; }

    [MaxLength(255)]
    public string? GhiChu { get; set; }

    public double? DiemThiLan1 { get; set; }

    public double? DiemThiLan2 { get; set; }

    public double? DiemTongKet { get; set; }

    public string? ChiTietDiemQT { get; set; }

    [ForeignKey("MaSV")]
    public virtual SinhVien? SinhVien { get; set; }

    [ForeignKey("MaLHP")]
    public virtual LopHocPhan? LopHocPhan { get; set; }
}
