using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_DiemSV.Models;

[Table("KhungChuongTrinh")]
public class KhungChuongTrinh
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [Required]
    [MaxLength(20)]
    public string MaNganh { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string MaMon { get; set; } = string.Empty;

    public int HocKyDuKien { get; set; }

    [MaxLength(50)]
    public string LoaiMon { get; set; } = "Bắt buộc";

    [ForeignKey("MaNganh")]
    public virtual Nganh? Nganh { get; set; }

    [ForeignKey("MaMon")]
    public virtual MonHoc? MonHoc { get; set; }
}
