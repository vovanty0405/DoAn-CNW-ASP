using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("Article")]
public class Article
{
    public int Id { get; set; }
    [Required, MaxLength(250)] public string TieuDe { get; set; } = "";
    [MaxLength(1000)] public string TomTat { get; set; } = "";
    [Required] public string NoiDung { get; set; } = "";
    [Required, MaxLength(40)] public string DanhMuc { get; set; } = "ThongBaoChung";
    [MaxLength(100)] public string HeDaoTao { get; set; } = "Đại học chính quy";
    [Required, MaxLength(200)] public string DonViDang { get; set; } = "";
    public int NguoiDangId { get; set; }
    public DateTime NgayDang { get; set; } = DateTime.UtcNow;
    public bool GhimDauTrang { get; set; }
    [MaxLength(500)] public string? FileDinhKemUrl { get; set; }
    [MaxLength(255)] public string? TenFileDinhKem { get; set; }
    public long? DungLuongFile { get; set; }
    [MaxLength(500)] public string? ThumbnailUrl { get; set; }
    [Required, MaxLength(20)] public string TrangThai { get; set; } = "BanNhap";
    public DateTime? XuatBanLuc { get; set; }
    [ForeignKey(nameof(NguoiDangId))] public UserAccount? NguoiDang { get; set; }
}

[Table("PortalDocument")]
public class PortalDocument
{
    public int Id { get; set; }
    [Required, MaxLength(250)] public string TieuDe { get; set; } = "";
    [Required, MaxLength(30)] public string Loai { get; set; } = "BieuMau";
    [Required, MaxLength(500)] public string FileUrl { get; set; } = "";
    public long DungLuongBytes { get; set; }
    public bool HienThi { get; set; } = true;
}
