using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QL_DiemSV.Models;

[Table("DieuKienMonHoc")]
public class DieuKienMonHoc
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [MaxLength(20)]
    public string? MaMon { get; set; }

    [MaxLength(20)]
    public string? MaMonTienQuyet { get; set; }

    [JsonIgnore]
    [ForeignKey("MaMon")]
    public virtual MonHoc? MonHocChinh { get; set; }

    [ForeignKey("MaMonTienQuyet")]
    public virtual MonHoc? MonHocTienQuyet { get; set; }
}
