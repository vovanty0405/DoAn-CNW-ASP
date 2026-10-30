using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Controllers;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.ViewModels;
public static class DatabaseChecks
{
    public static async Task Run()
    {
        string name = "AcademicLedger_Check_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=.\\SQLEXPRESS01;Database={name};Trusted_Connection=True;TrustServerCertificate=True").Options;
        await using var db = new ApplicationDbContext(options);
        try {
            DbInitializer.Initialize(db);
            AcademicSchemaUpgrade.Apply(db);
            void Assert(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS SQL " + label); }
            Assert(await db.LopHocPhans.AllAsync(x=>x.STC==x.MonHoc!.SoTinChi),"fresh seed credits match courses; upgrade is repeatable");
            var term = new HocKy { MaHK="CHECK",TenHK="Check",NamHocBatDau=2026,NamHocKetThuc=2027,NgayBatDauTuan1=new DateTime(2026,8,10),BatDauDangKy=DateTime.Now.AddDays(-1),KetThucDangKy=DateTime.Now.AddDays(1),SoTuan=20,DonGiaTinChi=635000 };
            db.HocKys.Add(term);
            db.PhongHocs.AddRange(new PhongHoc{MaPhong="CHECK1",SucChua=40},new PhongHoc{MaPhong="CHECK2",SucChua=20});
            db.MonHocs.AddRange(new MonHoc{MaMon="CHECK1",TenMon="Check 1",SoTinChi=3},new MonHoc{MaMon="CHECK2",TenMon="Check 2",SoTinChi=2},new MonHoc{MaMon="CHECK0",TenMon="Sinh hoạt chủ nhiệm",SoTinChi=0});
            db.LopHocPhans.AddRange(new LopHocPhan{MaLHP="CHECK1",MaMon="CHECK1",MaHK="CHECK",STC=3,STCHP=3,SiSoToiDa=2},new LopHocPhan{MaLHP="CHECK2",MaMon="CHECK2",MaHK="CHECK",STC=2,STCHP=2},new LopHocPhan{MaLHP="CHECK1B",MaMon="CHECK1",MaHK="CHECK",STC=3,STCHP=3},new LopHocPhan{MaLHP="CHECK0",MaMon="CHECK0",MaHK="CHECK",STC=0,STCHP=3});
            await db.SaveChangesAsync();
            LichHocController Admin() => new(db) { TempData = new TempDataDictionary(new DefaultHttpContext(),new MemoryTempData()) };
            ClassSchedule L(string section,string room="CHECK1", string teacher="GV001",string weeks="1-20",int start=1) => new(){MaLHP=section,PhongHoc=room,MaGV=teacher,ChuoiTuanHoc=weeks,TietBatDau=start,SoTiet=3,Thu=2};
            var admin=Admin();await admin.Save("CHECK1","01",null,3,3,null,2,new(){L("CHECK1")});
            Assert(await db.ClassSchedules.AnyAsync(x=>x.MaLHP=="CHECK1"),"save valid schedule");
            admin=Admin();await admin.Save("CHECK2","01",null,2,2,null,40,new(){L("CHECK2",teacher:"GV002")});
            Assert(admin.TempData.ContainsKey("Error")&&!await db.ClassSchedules.AnyAsync(x=>x.MaLHP=="CHECK2"),"reject room conflict");
            admin=Admin();await admin.Save("CHECK2","01",null,2,2,null,40,new(){L("CHECK2","CHECK2")});
            Assert(admin.TempData.ContainsKey("Error"),"reject teacher conflict");
            admin=Admin();await admin.Save("CHECK2","01",null,2,2,null,40,new(){L("CHECK2","CHECK2","GV002",start:4)});
            Assert(!admin.TempData.ContainsKey("Error")&&admin.TempData["Message"]!.ToString()!.Contains("20"),"adjacent schedule accepted with capacity warning");
            var ctx=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("MaSV","SV001"),new Claim(ClaimTypes.Role,"SinhVien")},"test"))};
            var student=new DangKyHocPhanController(db){ControllerContext=new ControllerContext{HttpContext=ctx}};
            async Task<IActionResult> Register(params string[] ids) => await student.Save(new RegistrationRequest{MaHK="CHECK",SectionIds=ids.ToList()});
            var result=await Register("CHECK1","CHECK0");
            Assert(result is OkObjectResult,"register selection");
            var paid=await db.KetQuaHocTaps.SingleAsync(x=>x.MaSV=="SV001"&&x.MaLHP=="CHECK1");
            Assert(paid.HocPhiDangKy==1905000,"tuition snapshot: 3 x 635000");
            Assert((await db.KetQuaHocTaps.SingleAsync(x=>x.MaSV=="SV001"&&x.MaLHP=="CHECK0")).HocPhiDangKy==0,"zero-credit course is free");
            Assert(await Register("CHECK1","CHECK1B") is BadRequestObjectResult,"reject duplicate groups");
            paid.DiemGK=0;await db.SaveChangesAsync();
            Assert(await Register("CHECK0") is BadRequestObjectResult,"reject cancellation of graded course");
            paid.DiemGK=null;await db.SaveChangesAsync();
            Assert(await Register() is OkObjectResult,"save cancellation");
            Assert(!await db.KetQuaHocTaps.AnyAsync(x=>x.MaSV=="SV001"&&x.LopHocPhan!.MaHK=="CHECK"),"cancellation persisted");
            admin=Admin();await admin.Save("CHECK2","01",null,2,2,null,40,new(){L("CHECK2","CHECK2","GV002")});
            Assert(!admin.TempData.ContainsKey("Error"),"different rooms and teachers can teach concurrently");
            Assert(await Register("CHECK1","CHECK2") is BadRequestObjectResult,"server rejects student schedule overlap");
            Assert(await Register("CHECK1") is OkObjectResult,"register before capacity check");
            var snapshot=await db.KetQuaHocTaps.SingleAsync(x=>x.MaSV=="SV001"&&x.MaLHP=="CHECK1");
            term.DonGiaTinChi=900000;await db.SaveChangesAsync();
            Assert(await Register("CHECK1") is OkObjectResult && snapshot.HocPhiDangKy==1905000,"saved tuition stays fixed after price changes");
            db.KetQuaHocTaps.Add(new KetQuaHocTap{MaSV="SV002",MaLHP="CHECK1"});await db.SaveChangesAsync();
            ctx.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("MaSV","SV003"),new Claim(ClaimTypes.Role,"SinhVien")},"test"));
            Assert(await Register("CHECK1") is BadRequestObjectResult,"server rejects full section");
            admin=Admin();await admin.Save("CHECK2","01",null,2,2,null,40,new(){L("CHECK2","CHECK2","GV002",weeks:"21")});
            Assert(admin.TempData.ContainsKey("Error"),"invalid semester weeks rejected");
            Assert((await db.ClassSchedules.SingleAsync(x=>x.MaLHP=="CHECK2")).ChuoiTuanHoc.Contains("20"),"invalid update preserves prior schedule");
            term.KetThucDangKy=DateTime.Now.AddDays(-1);await db.SaveChangesAsync();
            Assert(await Register("CHECK1") is BadRequestObjectResult,"server rejects closed registration");
            var classroom=await db.LopHanhChinhs.FirstAsync();
            admin=Admin();await admin.Save("CHECK1","01",null,3,3,classroom.MaLop,2,new(){L("CHECK1")});
            Assert((await db.LopHocPhans.FindAsync("CHECK1"))!.MaLop==classroom.MaLop,"administrative class selection persisted");
            var otherTerm=await db.HocKys.FirstAsync(x=>x.MaHK!="CHECK");
            var editor=(ScheduleEditorViewModel)((ViewResult)await Admin().Index("CHECK1",otherTerm.MaHK)).Model!;
            Assert(editor.AdministrativeClasses.Any(x=>x.MaLop==classroom.MaLop) && editor.Section.MaLop==classroom.MaLop,"GET loads administrative dropdown and selected value");
            Assert(editor.ConfigurationTerm!.MaHK==otherTerm.MaHK && editor.Section.MaHK=="CHECK","configuration term is independent of section term");
            admin=Admin();await admin.Room("ROOMNEW",50,"A","BaoTri");
            Assert((await db.PhongHocs.FindAsync("ROOMNEW"))!.TrangThai=="BaoTri","create room with building and status");
            admin=Admin();await admin.Room("ROOMRENAMED",60,"B","DangKhoa","ROOMNEW");
            Assert(await db.PhongHocs.FindAsync("ROOMNEW")==null && (await db.PhongHocs.FindAsync("ROOMRENAMED"))!.ToaNha=="B","rename and edit unused room");
            await Admin().DeleteRoom("ROOMRENAMED");
            Assert(!await db.PhongHocs.AnyAsync(x=>x.MaPhong=="ROOMRENAMED"),"delete unused room");
            admin=Admin();await admin.Room("CHECK2",20,"B","HoatDong","CHECK2");
            admin=Admin();await admin.Room("A-CHECK",40,"A","HoatDong","CHECK1");
            Assert(!await db.PhongHocs.AnyAsync(x=>x.MaPhong=="CHECK1") && await db.ClassSchedules.AnyAsync(x=>x.MaLHP=="CHECK1"&&x.PhongHoc=="A-CHECK"),"renaming used room migrates schedule foreign keys");
            admin=Admin();await admin.Room("A-CHECK",10,"C","HoatDong");
            Assert(admin.TempData.ContainsKey("Error") && (await db.PhongHocs.FindAsync("A-CHECK"))!.SucChua==40,"duplicate room code rejected without overwrite");
            admin=Admin();await admin.DeleteRoom("A-CHECK");
            Assert(admin.TempData.ContainsKey("Error") && await db.PhongHocs.AnyAsync(x=>x.MaPhong=="A-CHECK"),"cannot delete room with a schedule");
            var makeup=await db.ClassSchedules.SingleAsync(x=>x.MaLHP=="CHECK2");makeup.LoaiLich="DayBu";await db.SaveChangesAsync();
            async Task<RoomMatrixViewModel> Matrix(string mode="day",string type="all",string area="all",DateTime? date=null) => (RoomMatrixViewModel)((ViewResult)await Admin().Matrix("CHECK",1,2,mode,type,area,date)).Model!;
            var matrix=await Matrix(area:"A");
            Assert(matrix.Rooms.Count==1 && matrix.Schedules.Count==1 && matrix.Schedules[0].MaLHP=="CHECK1","building filter queries rooms and schedules");
            matrix=await Matrix(type:"DayBu");Assert(matrix.Schedules.Count==1 && matrix.Schedules[0].MaLHP=="CHECK2","schedule type filter");
            matrix=await Matrix(mode:"week");Assert(matrix.Days.Count==7,"weekly mode covers seven days");
            matrix=await Matrix(mode:"compact");Assert(matrix.Rooms.All(r=>matrix.Schedules.Any(x=>x.PhongHoc==r.MaPhong)),"compact mode shows only occupied rooms");
            matrix=await Matrix(date:new DateTime(2026,8,11));Assert(matrix.Week==1&&matrix.Day==3,"datepicker derives academic week and weekday");
            matrix=await Matrix(date:new DateTime(2026,8,9));Assert(matrix.OutsideTerm&&matrix.Week==0&&matrix.Schedules.Count==0,"date before semester never clamps to an unrelated week");
            admin=Admin();await admin.Room("A-CHECK",40,"A","BaoTri","A-CHECK");
            admin=Admin();await admin.Save("CHECK1","01",null,3,3,classroom.MaLop,2,new(){L("CHECK1","A-CHECK")});
            Assert(admin.TempData.ContainsKey("Error"),"maintenance room cannot be scheduled");
            admin=Admin();await admin.Term(otherTerm.MaHK,new DateTime(2026,8,10),new DateTime(2026,8,1),new DateTime(2026,8,9),20,700000,"CHECK1");
            Assert((await db.HocKys.FindAsync(otherTerm.MaHK))!.DonGiaTinChi==700000 && term.DonGiaTinChi==900000,"term configuration updates only the selected semester");
            await PortalChecks.Run(db);
        } finally { if (!name.StartsWith("AcademicLedger_Check_")) throw new Exception("Unsafe database cleanup"); await db.Database.EnsureDeletedAsync(); }
    }
    private class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
        public void SaveTempData(HttpContext context,IDictionary<string,object> values) { }
    }
}
