using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("NhatKyHoatDong")]
public class NhatKyHoatDong
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaLog { get; set; }

    [MaxLength(100)]
    public string? NguoiDung { get; set; }

    public DateTime ThoiGian { get; set; } = DateTime.Now;

    [MaxLength(255)]
    public string? HanhDong { get; set; }

    public string? ChiTiet { get; set; }

    [MaxLength(50)]
    public string? Loai { get; set; } = "Thông tin";
}
