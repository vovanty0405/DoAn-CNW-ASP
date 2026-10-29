using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("ThongBao")]
public class ThongBao
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaThongBao { get; set; }

    [MaxLength(20)]
    public string? MaNguoiGui { get; set; }

    [MaxLength(20)]
    public string? MaNguoiNhan { get; set; }

    [MaxLength(255)]
    public string? TieuDe { get; set; }

    public string? NoiDung { get; set; }

    [MaxLength(50)]
    public string? LoaiThongBao { get; set; }

    [MaxLength(50)]
    public string? ThamChieuID { get; set; }

    public DateTime NgayGui { get; set; } = DateTime.Now;

    public bool DaDoc { get; set; } = false;
}
