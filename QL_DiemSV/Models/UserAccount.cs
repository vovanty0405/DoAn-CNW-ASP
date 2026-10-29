using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("UserAccounts")]
public class UserAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public int? RoleID { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime NgayTao { get; set; } = DateTime.Now;

    [MaxLength(20)]
    public string? MaGV { get; set; }

    [ForeignKey("RoleID")]
    public virtual Role? Role { get; set; }

    public virtual GiangVien? GiangVien { get; set; }

    public virtual SinhVien? SinhVien { get; set; }

    [JsonIgnore]
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
