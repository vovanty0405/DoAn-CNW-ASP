using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Controllers;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
public static class SemesterChecks
{
    public static async Task Run()
    {
        var name="AcademicLedger_Check_"+Guid.NewGuid().ToString("N");
        await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=.\\SQLEXPRESS01;Database={name};Trusted_Connection=True;TrustServerCertificate=True").Options);
        int count=0;
        void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS SEMESTER "+text);}
        LichHocController Admin()=>new(db){TempData=new TempDataDictionary(new DefaultHttpContext(),new MemoryTempData())};
        var first=new DateTime(2026,8,10);var opens=new DateTime(2026,8,1,7,0,0);var closes=new DateTime(2026,8,8,12,0,0);
        try {
            DbInitializer.Initialize(db);
            var term=new HocKy{MaHK="SEMESTER_TEST",TenHK="Học kỳ thử",NamHocBatDau=2026,NamHocKetThuc=2027};db.HocKys.Add(term);await db.SaveChangesAsync();
            var editor=(ScheduleEditorViewModel)((ViewResult)await Admin().Index(null,term.MaHK)).Model!;
            Check(editor.Terms.Count==await db.HocKys.CountAsync()&&editor.ConfigurationTerm!.MaHK==term.MaHK,"GET includes every semester and preserves chosen configuration");
            var admin=Admin();await admin.Term(term.MaHK,first,opens,closes,20,635000);
            Check(!admin.TempData.ContainsKey("Error")&&term.NgayBatDauTuan1==first&&term.DonGiaTinChi==635000,"configure an existing semester for the first time");
            admin=Admin();await admin.Term(term.MaHK,first.AddDays(1),opens,closes,20,700000);
            Check(admin.TempData.ContainsKey("Error")&&term.DonGiaTinChi==635000,"reject non-Monday week one without changing prices");
            admin=Admin();await admin.Term(term.MaHK,first,closes,opens,20,700000);
            Check(admin.TempData.ContainsKey("Error")&&term.BatDauDangKy==opens,"reject reversed registration window");
            admin=Admin();await admin.Term(term.MaHK,default,opens,closes,20,700000);
            Check(admin.TempData.ContainsKey("Error")&&term.NgayBatDauTuan1==first,"reject omitted date rather than persisting year one");
            admin=Admin();await admin.Term(term.MaHK,first,opens,closes,20,-1);
            Check(admin.TempData.ContainsKey("Error")&&term.DonGiaTinChi==635000,"reject negative tuition");
            admin=Admin();await admin.Term(term.MaHK,first,opens,closes,21,700000);
            Check(!admin.TempData.ContainsKey("Error")&&term.SoTuan==21&&term.DonGiaTinChi==700000,"update existing configuration");
            var course=await db.MonHocs.FirstAsync();var teacher=await db.GiangViens.FirstAsync();
            db.PhongHocs.Add(new PhongHoc{MaPhong="SEM_ROOM",SucChua=40});
            db.LopHocPhans.Add(new LopHocPhan{MaLHP="SEM_SECTION",MaHK=term.MaHK,MaMon=course.MaMon});await db.SaveChangesAsync();
            db.ClassSchedules.Add(new ClassSchedule{MaLHP="SEM_SECTION",MaGV=teacher.MaGV,PhongHoc="SEM_ROOM",ChuoiTuanHoc="21",Thu=2,TietBatDau=1,SoTiet=3});await db.SaveChangesAsync();
            admin=Admin();await admin.Term(term.MaHK,first,opens,closes,20,1);
            Check(admin.TempData.ContainsKey("Error")&&term.SoTuan==21&&term.DonGiaTinChi==700000,"cannot shrink semester below scheduled weeks");
            admin=Admin();await admin.DeleteTerm(term.MaHK);
            Check(admin.TempData.ContainsKey("Error")&&await db.HocKys.AnyAsync(x=>x.MaHK==term.MaHK),"delete blocked when semester has a course section");
            var empty=new HocKy{MaHK="SEM_EMPTY",TenHK="Học kỳ trống"};db.HocKys.Add(empty);await db.SaveChangesAsync();
            admin=Admin();await admin.DeleteTerm(empty.MaHK,configHK:empty.MaHK);
            Check(!admin.TempData.ContainsKey("Error")&&!await db.HocKys.AnyAsync(x=>x.MaHK==empty.MaHK),"unused semester can be deleted");
            admin=Admin();await admin.DeleteTerm("MISSING");Check(admin.TempData.ContainsKey("Error"),"missing semester deletion shows an explanatory message");
            Check(TermConfigurationPresentation.Status(new HocKy(),first).Label=="Chưa cấu hình","unconfigured semester status is explicit");
            Check(TermConfigurationPresentation.Status(term,first.AddDays(-1)).Label=="Sắp tới","status before semester begins");
            Check(TermConfigurationPresentation.Status(term,first).Label=="Hiện tại","status on first day");
            Check(TermConfigurationPresentation.Status(term,first.AddDays(147)).Label=="Đã kết thúc","end boundary follows configured number of weeks");
            Console.WriteLine($"{count} semester checks passed.");
        }finally{if(!name.StartsWith("AcademicLedger_Check_"))throw new Exception("Unsafe database cleanup");await db.Database.EnsureDeletedAsync();}
    }
    private class MemoryTempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){}}
}
