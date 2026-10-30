using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("LopHocPhan")]
public class LopHocPhan
{
    [Key]
    [MaxLength(20)]
    public string MaLHP { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MaMon { get; set; }

    [MaxLength(20)]
    public string? MaHK { get; set; }

    [MaxLength(20)]
    public string? MaGV { get; set; }

    [MaxLength(255)]
    public string? TenLopHP { get; set; }

    [MaxLength(50)]
    public string? PhongHoc { get; set; }

    public int? SiSoToiDa { get; set; } = 40;

    [MaxLength(10)] public string NMH { get; set; } = "01";
    [MaxLength(10)] public string? TTH { get; set; }
    [Range(0, 30)] public int STC { get; set; }
    [Range(0, 100)] [Column(TypeName = "decimal(8,2)")] public decimal STCHP { get; set; }
    [MaxLength(20)] public string? MaLop { get; set; }
    [NotMapped] public int SoLuongDaDangKy => KetQuaHocTaps.Count;
    [JsonIgnore] public ICollection<ClassSchedule> LichHocs { get; set; } = new List<ClassSchedule>();

    [MaxLength(50)]
    public string? TrangThai { get; set; } = "MoDangKy";

    public string? CauHinhDiem { get; set; }

    [MaxLength(50)]
    public string? TrangThaiNhapDiem { get; set; } = "Vui lòng nhập điểm";

    [ForeignKey("MaMon")]
    public virtual MonHoc? MonHoc { get; set; }

    [ForeignKey("MaHK")]
    public virtual HocKy? HocKy { get; set; }

    [ForeignKey("MaGV")]
    public virtual GiangVien? GiangVien { get; set; }

    [JsonIgnore]
    public virtual ICollection<KetQuaHocTap> KetQuaHocTaps { get; set; } = new List<KetQuaHocTap>();

    [JsonIgnore]
    public virtual ICollection<DonKhieuNai> DonKhieuNais { get; set; } = new List<DonKhieuNai>();
}
