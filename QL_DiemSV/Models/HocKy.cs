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

    [JsonIgnore]
    public virtual ICollection<LopHocPhan> LopHocPhans { get; set; } = new List<LopHocPhan>();
}
