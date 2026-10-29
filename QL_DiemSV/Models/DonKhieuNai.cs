using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("DonKhieuNai")]
public class DonKhieuNai
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaKN { get; set; }

    [Required]
    [MaxLength(20)]
    public string MaSV { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string MaLHP { get; set; } = string.Empty;

    [Required]
    public string LyDo { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LoaiDiem { get; set; } = "quatrinh";

    public DateTime NgayGui { get; set; } = DateTime.Now;

    [MaxLength(50)]
    public string TrangThai { get; set; } = "pending";

    public string? PhanHoi { get; set; }

    public bool DaXem { get; set; } = false;

    [ForeignKey("MaSV")]
    public virtual SinhVien? SinhVien { get; set; }

    [ForeignKey("MaLHP")]
    public virtual LopHocPhan? LopHocPhan { get; set; }
}
