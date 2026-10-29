using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("SinhVien")]
public class SinhVien
{
    [Key]
    [MaxLength(20)]
    public string MaSV { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string HoTen { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime? NgaySinh { get; set; }

    [MaxLength(10)]
    public string? GioiTinh { get; set; }

    [MaxLength(255)]
    public string? DiaChi { get; set; }

    [MaxLength(20)]
    public string? CCCD { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? SDT { get; set; }

    [MaxLength(20)]
    public string? MaLop { get; set; }

    [MaxLength(50)]
    public string? TrangThai { get; set; } = "Đang học";

    public int? UserID { get; set; }

    [ForeignKey("MaLop")]
    public virtual LopHanhChinh? LopHanhChinh { get; set; }

    [JsonIgnore]
    [ForeignKey("UserID")]
    public virtual UserAccount? UserAccount { get; set; }

    [JsonIgnore]
    public virtual ICollection<KetQuaHocTap> KetQuaHocTaps { get; set; } = new List<KetQuaHocTap>();

    [JsonIgnore]
    public virtual ICollection<DonKhieuNai> DonKhieuNais { get; set; } = new List<DonKhieuNai>();
}
