using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("Roles")]
public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoleID { get; set; }

    [Required]
    [MaxLength(50)]
    public string RoleName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? MoTa { get; set; }

    [JsonIgnore]
    public virtual ICollection<UserAccount> UserAccounts { get; set; } = new List<UserAccount>();

    [JsonIgnore]
    public virtual ICollection<PhanQuyen> PhanQuyens { get; set; } = new List<PhanQuyen>();

    [JsonIgnore]
    public virtual ICollection<PhanQuyenPhamVi> PhanQuyenPhamVis { get; set; } = new List<PhanQuyenPhamVi>();
}
