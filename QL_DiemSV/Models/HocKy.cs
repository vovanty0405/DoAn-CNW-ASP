using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("HocKy")]
public class HocKy
{
    [Key]
    [MaxLength(20)]
    public string MaHK { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TenHK { get; set; } = string.Empty;

    public int NamHocBatDau { get; set; }

    public int NamHocKetThuc { get; set; }

    public DateTime? NgayBatDauTuan1 { get; set; }
    public DateTime? BatDauDangKy { get; set; }
    public DateTime? KetThucDangKy { get; set; }
    [Range(1, 52)] public int SoTuan { get; set; } = 20;
    [Column(TypeName = "decimal(18,2)")] [Range(0, 100000000)] public decimal DonGiaTinChi { get; set; } = 635000;

    [JsonIgnore]
    public virtual ICollection<LopHocPhan> LopHocPhans { get; set; } = new List<LopHocPhan>();
}
