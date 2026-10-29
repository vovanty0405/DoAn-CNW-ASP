using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("LopHanhChinh")]
public class LopHanhChinh
{
    [Key]
    [MaxLength(20)]
    public string MaLop { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string TenLop { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? NienKhoa { get; set; }

    [MaxLength(20)]
    public string? MaNganh { get; set; }

    [MaxLength(20)]
    public string? MaGVCN { get; set; }

    [ForeignKey("MaNganh")]
    public virtual Nganh? Nganh { get; set; }

    [ForeignKey("MaGVCN")]
    public virtual GiangVien? GiangVien { get; set; }

    [JsonIgnore]
    public virtual ICollection<SinhVien> SinhViens { get; set; } = new List<SinhVien>();
}
