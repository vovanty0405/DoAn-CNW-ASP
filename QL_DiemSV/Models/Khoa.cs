using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("Khoa")]
public class Khoa
{
    [Key]
    [MaxLength(20)]
    public string MaKhoa { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string TenKhoa { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime? NgayThanhLap { get; set; }

    [MaxLength(100)]
    public string? TruongKhoa { get; set; }

    [JsonIgnore]
    public virtual ICollection<Nganh> Nganhs { get; set; } = new List<Nganh>();

    [JsonIgnore]
    public virtual ICollection<MonHoc> MonHocs { get; set; } = new List<MonHoc>();

    [JsonIgnore]
    public virtual ICollection<GiangVien> GiangViens { get; set; } = new List<GiangVien>();
}
