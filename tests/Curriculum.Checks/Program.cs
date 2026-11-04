using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Controllers;
using QL_DiemSV.Data;
using QL_DiemSV.ViewModels;
var name="AcademicLedger_Check_"+Guid.NewGuid().ToString("N");
await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=.\\SQLEXPRESS01;Database={name};Trusted_Connection=True;TrustServerCertificate=True").Options);
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
AdminCurriculumController Admin()=>new(db){TempData=new TempDataDictionary(new DefaultHttpContext(),new MemoryTempData())};
try {
 DbInitializer.Initialize(db);AcademicSchemaUpgrade.Apply(db);
 var major=await db.Nganhs.FirstAsync();
 CurriculumEditViewModel Input()=>new(){HeDaoTao="Chính quy",KhoaHoc=2026,NganhHocId=major.MaNganh,TenChuongTrinh="Việt Nhật",TrangThai=true,Contents=Enumerable.Repeat("<p>Thông tin <script>bad()</script></p>",11).ToList()};
 Check(await Admin().Save(Input()) is RedirectToActionResult,"create curriculum");
 var c=await db.Curriculums.Include(x=>x.Sections).SingleAsync();Check(c.Sections.Count==11&&!c.Sections.Any(x=>x.NoiDung.Contains("script")),"eleven ordered sections and safe HTML");
 Check(await Admin().Save(Input()) is ViewResult,"reject duplicate curriculum");
 var partial=Input();partial.Id=c.Id;partial.Contents=new(){"<p>Đã cập nhật một mục</p>"};
 var originalLast=c.Sections.Single(x=>x.ThuTu==11).NoiDung;
 Check(await Admin().Save(partial) is RedirectToActionResult && c.Sections.Single(x=>x.ThuTu==11).NoiDung==originalLast,"save one section preserves omitted existing sections");
 var empty=Input();empty.Id=c.Id;empty.Contents=new();
 Check(await Admin().Save(empty) is RedirectToActionResult,"save with no submitted sections");
 var large=Input();large.Id=c.Id;large.Contents=new(){new string('A',150000)};
 Check(await Admin().Save(large) is RedirectToActionResult && c.Sections.Single(x=>x.ThuTu==1).NoiDung.Length>=150000,"save content exceeding previous 100000 limit");
 var tooLarge=Input();tooLarge.Id=c.Id;tooLarge.Contents=new(){new string('A',2000001)};
 Check(await Admin().Save(tooLarge) is ViewResult,"oversized content returns a specific validation error");
 var clear=Input();clear.Id=c.Id;clear.Contents=new(){""};
 Check(await Admin().Save(clear) is RedirectToActionResult && c.Sections.Single(x=>x.ThuTu==1).NoiDung=="","explicit empty section is allowed");

 var portal=new PortalController(db){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
 var catalog=(CurriculumCatalogViewModel)((ViewResult)await portal.Curriculums(null,null,null,null)).Model!;Check(catalog.All.Count==1,"published catalog");
 await Admin().Clone(c.Id,2027);var copy=await db.Curriculums.Include(x=>x.Sections).SingleAsync(x=>x.KhoaHoc==2027);Check(!copy.TrangThai&&copy.Sections.Count==11&&copy.Sections.All(x=>x.CurriculumId==copy.Id),"clone is independent and hidden");
 Check(await portal.CurriculumDetail(copy.Id) is NotFoundResult,"hidden curriculum cannot be viewed anonymously");
 Check(await portal.CurriculumDetail(c.Id) is ViewResult,"public curriculum detail");
 var edit=Input();edit.Id=c.Id;edit.TrangThai=false;await Admin().Save(edit);
 catalog=(CurriculumCatalogViewModel)((ViewResult)await portal.Curriculums(null,null,null,null)).Model!;Check(catalog.All.Count==0,"hidden curricula excluded from catalog");
 await Admin().Delete(c.Id);Check(!await db.CurriculumSections.AnyAsync(x=>x.CurriculumId==c.Id)&&await db.CurriculumSections.CountAsync()==11,"delete cascades only own sections");
}finally {if(!name.StartsWith("AcademicLedger_Check_"))throw new Exception("Unsafe cleanup");await db.Database.EnsureDeletedAsync();}
class MemoryTempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){}}
