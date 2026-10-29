using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("MonHoc")]
public class MonHoc
{
    [Key]
    [MaxLength(20)]
    public string MaMon { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string TenMon { get; set; } = string.Empty;

    public int SoTinChi { get; set; }

    public int? SoTietLyThuyet { get; set; }

    public int? SoTietThucHanh { get; set; }

    [MaxLength(20)]
    public string? MaKhoa { get; set; }

    [ForeignKey("MaKhoa")]
    public virtual Khoa? Khoa { get; set; }

    public virtual ICollection<DieuKienMonHoc> DieuKienTienQuyets { get; set; } = new List<DieuKienMonHoc>();

    [JsonIgnore]
    public virtual ICollection<DieuKienMonHoc> LaTienQuyetCua { get; set; } = new List<DieuKienMonHoc>();

    [JsonIgnore]
    public virtual ICollection<KhungChuongTrinh> KhungChuongTrinhs { get; set; } = new List<KhungChuongTrinh>();

    [JsonIgnore]
    public virtual ICollection<LopHocPhan> LopHocPhans { get; set; } = new List<LopHocPhan>();
}
