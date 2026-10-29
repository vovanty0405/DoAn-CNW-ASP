using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("ChucNang")]
public class ChucNang
{
    [Key]
    [MaxLength(50)]
    public string MaChucNang { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TenChucNang { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? MoTa { get; set; }

    [JsonIgnore]
    public virtual ICollection<PhanQuyen> PhanQuyens { get; set; } = new List<PhanQuyen>();
}
