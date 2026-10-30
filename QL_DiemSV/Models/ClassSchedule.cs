using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace QL_DiemSV.Models;
[Table("ClassSchedule")]
public class ClassSchedule
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string MaLHP { get; set; } = "";
    [Required, MaxLength(20)] public string LoaiLich { get; set; } = "LichHoc";
    public bool LoaiBuoiHoc { get; set; }
    [Range(2,8)] public int Thu { get; set; } = 2;
    [Range(1,12)] public int TietBatDau { get; set; } = 1;
    [Range(1,12)] public int SoTiet { get; set; } = 3;
    [Required, MaxLength(50)] public string PhongHoc { get; set; } = "";
    [Required, MaxLength(20)] public string MaGV { get; set; } = "";
    [MaxLength(200)] public string? TuanBaoNghi { get; set; } = "";
    // Canonical comma-separated week numbers, e.g. 1,2,6,8,9,10,11,12.
    [Required, MaxLength(200)] public string ChuoiTuanHoc { get; set; } = "1-20";
    [ForeignKey(nameof(MaLHP)), JsonIgnore] public LopHocPhan? LopHocPhan { get; set; }
    [ForeignKey(nameof(MaGV)), JsonIgnore] public GiangVien? GiangVien { get; set; }
}
[Table("PhongHoc")]
public class PhongHoc
{
    [Key, MaxLength(50)] public string MaPhong { get; set; } = "";
    [Required, MaxLength(30)] public string ToaNha { get; set; } = "Khac";
    [Required, MaxLength(20)] public string TrangThai { get; set; } = "HoatDong";
    [Range(1,10000)] public int SucChua { get; set; } = 40;
}
