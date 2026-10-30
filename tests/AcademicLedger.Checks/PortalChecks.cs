using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QL_DiemSV.Controllers;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
public static class PortalChecks
{
    public static async Task Run(ApplicationDbContext db)
    {
        int count=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS PORTAL "+message);}
        var gv=await db.UserAccounts.SingleAsync(x=>x.Username=="GV001");var admin=await db.UserAccounts.SingleAsync(x=>x.Username=="admin");
        var root=Path.Combine(AppContext.BaseDirectory,"portal-fixture-"+Guid.NewGuid().ToString("N"));
        var env=new TestEnvironment{ContentRootPath=root};
        ArticleController Writer(int id,string role) => new(db,env){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,role)},"test"))}},TempData=new TempDataDictionary(new DefaultHttpContext(),new MemoryTempData())};
        var anonymous=Writer(0,"Guest");var teacher=Writer(gv.UserID,"GiangVien");
        ArticleEditViewModel Draft()=>new(){TieuDe="Thông báo thử nghiệm Portal",TomTat="Tóm tắt",NoiDung="<p onclick=\"bad()\">Lịch mới <strong>quan trọng</strong><script>alert(1)</script></p>",DanhMuc="NghiBu",DonViDang="Giảng viên kiểm thử",TrangThai="BanNhap"};
        try{
            var draft=Draft();var save=await teacher.Save(draft);Check(save is RedirectToActionResult,"author can save draft");
            var article=await db.Articles.SingleAsync(x=>x.TieuDe==draft.TieuDe);
            Check(article.NguoiDangId==gv.UserID&&!article.NoiDung.Contains("script")&&!article.NoiDung.Contains("onclick"),"author id comes from claims; HTML is sanitized");
            Check(await anonymous.Details(article.Id) is NotFoundResult,"anonymous cannot read draft");
            Check(await teacher.Details(article.Id) is ViewResult,"author can preview draft");
            var portal=new PortalController(db){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};var home=(PortalViewModel)((ViewResult)await portal.Index()).Model!;
            Check(!home.Articles.Any(x=>x.Id==article.Id),"draft not included in public portal");
            var stranger=Writer(admin.UserID,"GiangVien");Check(await stranger.Edit(article.Id) is ForbidResult,"other lecturer cannot edit article");
            Check(await stranger.Pin(article.Id) is ForbidResult,"other lecturer cannot pin article");
            Check(await stranger.Delete(article.Id) is ForbidResult,"other lecturer cannot delete article");
            var edit=Draft();edit.Id=article.Id;edit.TrangThai="DaXuatBan";edit.GhimDauTrang=true;
            await teacher.Save(edit);Check(article.XuatBanLuc.HasValue,"publishing records publication timestamp");
            home=(PortalViewModel)((ViewResult)await portal.Index("NghiBu")).Model!;
            Check(home.Articles.Any(x=>x.Id==article.Id)&&home.Counts["NghiBu"]==1,"published article appears in filtered portal and counts");
            Check(await anonymous.Details(article.Id) is ViewResult,"public can read published article");
            var managed=(ArticleListViewModel)((ViewResult)await stranger.Index(null,null,null)).Model!;Check(!managed.Articles.Any(x=>x.Id==article.Id),"lecturer list only includes own articles");
            managed=(ArticleListViewModel)((ViewResult)await Writer(admin.UserID,"Admin").Index(null,null,null)).Model!;Check(managed.Articles.Any(x=>x.Id==article.Id),"administrator can list all articles");
            using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Attachment test"));
            var upload=Draft();upload.TieuDe="Bài có tệp";upload.File=new FormFile(stream,0,stream.Length,"File","test.txt");
            await teacher.Save(upload);var attached=await db.Articles.SingleAsync(x=>x.TieuDe==upload.TieuDe);
            var name=attached.FileDinhKemUrl!.Split('/').Last();
            Check(await anonymous.Attachment(name) is NotFoundResult,"draft attachment remains private");
            Check(await teacher.Attachment(name) is PhysicalFileResult,"author can download attached file");
            Check(await anonymous.Attachment("../appsettings.json") is NotFoundResult,"attachment path traversal rejected");
            var lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("SV001","student","CHECK",1)).Model!;
            Check(lookup.FullName!=null&&lookup.Schedules.All(x=>x.MaLHP=="CHECK1"),"student lookup joins registrations and returns own schedule");
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("GV001","lecturer","CHECK",1)).Model!;
            Check(lookup.FullName!=null&&lookup.Schedules.All(x=>x.MaGV=="GV001"),"lecturer lookup filters session instructor");
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("NOT-A-STUDENT","student","CHECK",1)).Model!;
            Check(lookup.Error!=null,"unknown student displays validation message");
            var initial=(PortalTimetableViewModel)((ViewResult)await portal.Timetable(null)).Model!;
            Check(!initial.HasResult&&initial.HocKys.Count==0&&initial.TermSchedules.Count==0,"initial lookup does not load academic filters or schedule");
            Check(!lookup.HasResult&&lookup.HocKys.Count==0,"invalid code remains in lookup step");
            var student=await db.SinhViens.SingleAsync(x=>x.MaSV=="SV001");
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("SV001","student","CHECK",1)).Model!;
            Check(lookup.DateOfBirth==student.NgaySinh&&!string.IsNullOrWhiteSpace(lookup.FacultyName),"student profile includes date of birth and faculty");
            var instructor=await db.GiangViens.SingleAsync(x=>x.MaGV=="GV001");
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("GV001","lecturer","CHECK",1)).Model!;
            Check(lookup.Degree==instructor.HocVi&&lookup.Email==instructor.Email,"lecturer profile includes degree and email");
            var extra=new ClassSchedule{MaLHP="CHECK1",MaGV="GV001",PhongHoc="A-CHECK",Thu=4,TietBatDau=4,SoTiet=2,ChuoiTuanHoc="2",TuanBaoNghi="2"};
            db.ClassSchedules.Add(extra);await db.SaveChangesAsync();
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("SV001","student","CHECK",1)).Model!;
            Check(!lookup.Schedules.Any(x=>x.Id==extra.Id),"weekly mode excludes other weeks");
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("SV001","student","CHECK",1,"all")).Model!;
            Check(lookup.Mode=="all"&&lookup.Schedules.Any(x=>x.Id==extra.Id)&&lookup.StatusClass(lookup.Schedules.Single(x=>x.Id==extra.Id))=="cancelled","whole semester mode includes other weeks and actual absence state");
            var monday=RoomMatrixRules.Today.AddDays(2-RoomMatrixRules.AcademicDay(RoomMatrixRules.Today));
            db.HocKys.Add(new HocKy{MaHK="LOOKUP_CURRENT",TenHK="Học kỳ hiện tại",NgayBatDauTuan1=monday,SoTuan=20});await db.SaveChangesAsync();
            lookup=(PortalTimetableViewModel)((ViewResult)await portal.Timetable("SV001")).Model!;
            Check(lookup.HocKy!.MaHK=="LOOKUP_CURRENT"&&lookup.Week==1,"successful lookup defaults to today's semester and week");
            Check(lookup.LoadedAt.Date==RoomMatrixRules.Today,"snapshot timestamp uses Vietnam date");
            var major=await db.Nganhs.FirstAsync();Check(await portal.Curriculum(major.MaNganh) is ViewResult,"curriculum reads existing major and training framework");
            var bad=Draft();bad.DanhMuc="Bogus";Check(await Writer(gv.UserID,"GiangVien").Save(bad) is ViewResult,"invalid category rejected");
            await teacher.Delete(attached.Id);Check(!System.IO.File.Exists(Path.Combine(root,"App_Data","PortalFiles",name)),"deleting article removes its attachment");
            await teacher.Delete(article.Id);Check(!await db.Articles.AnyAsync(),"article deletion persists");
        }finally{
            var full=Path.GetFullPath(root);if(!full.StartsWith(Path.GetFullPath(AppContext.BaseDirectory),StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture path");
            if(Directory.Exists(full))Directory.Delete(full,true);
        }
        Console.WriteLine($"{count} portal database checks passed.");
    }
    private class MemoryTempData:ITempDataProvider {public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){}}
    private class TestEnvironment:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="QL_DiemSV";public string EnvironmentName{get;set;}="Development";public string WebRootPath{get;set;}="";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
