using System.ComponentModel.DataAnnotations;
using QL_DiemSV.Models;
namespace QL_DiemSV.ViewModels;
public class CurriculumEditViewModel {
 public int Id {get;set;}
 [Required,MaxLength(80)] public string HeDaoTao {get;set;}="Chính quy";
 [Range(1900,2200)] public int KhoaHoc {get;set;}=DateTime.Now.Year;
 [Required,MaxLength(20)] public string NganhHocId {get;set;}="";
 [Required,MaxLength(200)] public string TenChuongTrinh {get;set;}="Chính quy";
 public bool TrangThai {get;set;}=true;
 public List<string> Contents {get;set;}=new();
 public List<Nganh> Majors {get;set;}=new();
}
public class CurriculumCatalogViewModel {
 public Dictionary<int,int> SectionCounts {get;set;}=new();
 public int Page {get;set;}=1;
 public int PageSize {get;set;}=20;
 public int Total {get;set;}
 public int TotalPages => Math.Max(1,(Total+PageSize-1)/PageSize);
 public List<Curriculum> All {get;set;}=new();
 public List<Curriculum> Visible {get;set;}=new();
 public string? System {get;set;}
 public int? Year {get;set;}
 public string? Program {get;set;}
 public string? Search {get;set;}
 public string? Major {get;set;}
 public bool? Active {get;set;}
}
public class CurriculumDetailViewModel {
 public Curriculum Curriculum {get;set;}=new();
 public List<Curriculum> Alternatives {get;set;}=new();
}
