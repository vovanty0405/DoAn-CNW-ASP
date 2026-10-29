using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("HanhDong")]
public class HanhDong
{
    [Key]
    [MaxLength(50)]
    public string MaHanhDong { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TenHanhDong { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? MoTa { get; set; }

    [JsonIgnore]
    public virtual ICollection<PhanQuyen> PhanQuyens { get; set; } = new List<PhanQuyen>();
}
