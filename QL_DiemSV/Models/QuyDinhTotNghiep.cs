using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("QuyDinhTotNghiep")]
public class QuyDinhTotNghiep
{
    [Key]
    [MaxLength(20)]
    public string MaNganh { get; set; } = string.Empty;

    public int TongTTC { get; set; } = 130;

    public int TCBatBuoc { get; set; } = 100;

    public int TCTuChon { get; set; } = 30;

    public double MinGPA { get; set; } = 2.0;

    [JsonIgnore]
    [ForeignKey("MaNganh")]
    public virtual Nganh? Nganh { get; set; }
}
