using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("GiangVien")]
public class GiangVien
{
    [Key]
    [MaxLength(20)]
    public string MaGV { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string HoTen { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime? NgaySinh { get; set; }

    [MaxLength(10)]
    public string? GioiTinh { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? SDT { get; set; }

    [MaxLength(100)]
    public string? HocVi { get; set; }

    [MaxLength(20)]
    public string? MaKhoa { get; set; }

    public int? UserID { get; set; }

    [MaxLength(20)]
    public string? CCCD { get; set; }

    [ForeignKey("MaKhoa")]
    public virtual Khoa? Khoa { get; set; }

    [JsonIgnore]
    [ForeignKey("UserID")]
    public virtual UserAccount? UserAccount { get; set; }

    [JsonIgnore]
    public virtual ICollection<LopHanhChinh> LopHanhChinhs { get; set; } = new List<LopHanhChinh>();

    [JsonIgnore]
    public virtual ICollection<LopHocPhan> LopHocPhans { get; set; } = new List<LopHocPhan>();
}
