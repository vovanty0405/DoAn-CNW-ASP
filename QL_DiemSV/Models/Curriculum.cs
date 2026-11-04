using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace QL_DiemSV.Models;
public class Curriculum {
 public int Id {get;set;}
 [Required,MaxLength(80)] public string HeDaoTao {get;set;}="Chính quy";
 [Range(1900,2200)] public int KhoaHoc {get;set;}=2026;
 [Required,MaxLength(20)] public string NganhHocId {get;set;}="";
 [ForeignKey(nameof(NganhHocId))] public Nganh? NganhHoc {get;set;}
 [Required,MaxLength(200)] public string TenChuongTrinh {get;set;}="Chính quy";
 public bool TrangThai {get;set;}=true;
 public DateTime NgayTao {get;set;}=DateTime.UtcNow;
 public DateTime NgayCapNhat {get;set;}=DateTime.UtcNow;
 public List<CurriculumSection> Sections {get;set;}=new();
}
public class CurriculumSection {
 public int Id {get;set;}
 public int CurriculumId {get;set;}
 public Curriculum? Curriculum {get;set;}
 [Range(1,11)] public int ThuTu {get;set;}
 [Required,MaxLength(250)] public string TieuDeMuc {get;set;}="";
 public string NoiDung {get;set;}="";
}
