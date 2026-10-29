using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("Nganh")]
public class Nganh
{
    [Key]
    [MaxLength(20)]
    public string MaNganh { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string TenNganh { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MaKhoa { get; set; }

    [ForeignKey("MaKhoa")]
    public virtual Khoa? Khoa { get; set; }

    public virtual QuyDinhTotNghiep? QuyDinhTotNghiep { get; set; }

    [JsonIgnore]
    public virtual ICollection<KhungChuongTrinh> KhungChuongTrinhs { get; set; } = new List<KhungChuongTrinh>();

    [JsonIgnore]
    public virtual ICollection<LopHanhChinh> LopHanhChinhs { get; set; } = new List<LopHanhChinh>();
}
