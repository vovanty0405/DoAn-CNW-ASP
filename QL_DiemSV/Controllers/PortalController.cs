using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;
namespace QL_DiemSV.Controllers;
[AllowAnonymous,Route("portal")]
public class PortalController(ApplicationDbContext db):Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string category="all",string? search=null,int page=1)
    {
        var published=db.Articles.AsNoTracking().Where(x=>x.TrangThai=="DaXuatBan");
        var counts=await published.GroupBy(x=>x.DanhMuc).Select(g=>new{g.Key,Count=g.Count()}).ToDictionaryAsync(x=>x.Key,x=>x.Count);
        var q=published;
        if(PortalRules.Categories.ContainsKey(category))q=q.Where(x=>x.DanhMuc==category);else category="all";
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.TieuDe.Contains(search)||x.TomTat.Contains(search));
        int total=await q.CountAsync();page=Math.Clamp(page,1,Math.Max(1,(total+11)/12));
        var now=PortalRules.Local(DateTime.UtcNow);var date=now.Date;
        var terms=await db.HocKys.AsNoTracking().ToListAsync();
        var current=RoomMatrixRules.ResolveCalendar(terms,null,null,null,null,date);
        var rooms=await db.PhongHocs.AsNoTracking().OrderBy(x=>x.MaPhong).Take(8).ToListAsync();
        var lessons=new List<ClassSchedule>();
        if(current.Term!=null&&!current.OutsideTerm&&current.Term.NgayBatDauTuan1.HasValue) lessons=(await db.ClassSchedules.AsNoTracking().Where(x=>x.LopHocPhan!.MaHK==current.Term.MaHK&&x.Thu==current.Day).ToListAsync()).Where(x=>ScheduleRules.Weeks(x.ChuoiTuanHoc).Contains(current.Week)).ToList();
        var active=Enumerable.Range(1,12).FirstOrDefault(t=>{var times=RoomMatrixRules.PeriodTime(t).Split(" - ");var start=TimeOnly.Parse(times[0]);var end=TimeOnly.Parse(times[1]);var time=TimeOnly.FromDateTime(now);return time>=start&&time<end;});
        return View(new PortalViewModel{Category=category,Search=search,Page=page,Total=total,Counts=counts,
            Articles=await q.OrderByDescending(x=>x.GhimDauTrang).ThenByDescending(x=>x.NgayDang).Skip((page-1)*12).Take(12).ToListAsync(),
            News=await published.Where(x=>x.DanhMuc=="TinTuc").OrderByDescending(x=>x.NgayDang).Take(3).ToListAsync(),
            Majors=await db.Nganhs.AsNoTracking().Include(x=>x.KhungChuongTrinhs).ThenInclude(x=>x.MonHoc).OrderBy(x=>x.HeDaoTao).ThenBy(x=>x.TenNganh).ToListAsync(),
            Documents=await db.PortalDocuments.AsNoTracking().Where(x=>x.HienThi).OrderBy(x=>x.TieuDe).ToListAsync(),
            Rooms=rooms.Select(r=>{var booked=lessons.Where(x=>x.PhongHoc==r.MaPhong).ToList();return new RoomTodayRow(r.MaPhong,string.Join(", ",booked.Select(x=>$"{x.TietBatDau}–{x.TietBatDau+x.SoTiet-1}")),r.TrangThai!="HoatDong"?RoomMatrixRules.Statuses.GetValueOrDefault(r.TrangThai,r.TrangThai):current.Term?.NgayBatDauTuan1==null||current.OutsideTerm?"Chưa có lịch":booked.Any(x=>x.TietBatDau<=active&&active<x.TietBatDau+x.SoTiet)?"Đang học":"Trống");}).ToList()
        });
    }
    [HttpGet("chuong-trinh/{id}")]
    public async Task<IActionResult> Curriculum(string id)
    {
        var major=await db.Nganhs.AsNoTracking().Include(x=>x.Khoa).Include(x=>x.KhungChuongTrinhs).ThenInclude(x=>x.MonHoc).FirstOrDefaultAsync(x=>x.MaNganh==id);
        return major==null?NotFound():View(major);
    }
    [HttpGet("tra-cuu-tkb")]
    public async Task<IActionResult> Timetable(string? code,string kind="student",string? maHK=null,int? week=null,string mode="week")
    {
        var vm=new PortalTimetableViewModel { Code=code?.Trim(),Kind=kind=="lecturer"?"lecturer":"student",Mode=mode=="all"?"all":"week" };
        // Step 1: no academic filters or schedule queries until a real profile is resolved.
        if(string.IsNullOrWhiteSpace(vm.Code))return View(vm);
        if(vm.Code.Length>20){vm.Error="Mã sinh viên/giảng viên tối đa 20 ký tự.";return View(vm);}
        if(vm.Kind=="student") {
            var person=await db.SinhViens.AsNoTracking().Include(x=>x.LopHanhChinh).ThenInclude(x=>x!.Nganh).ThenInclude(x=>x!.Khoa).FirstOrDefaultAsync(x=>x.MaSV==vm.Code);
            if(person==null){vm.Error="Không tìm thấy mã sinh viên. Vui lòng kiểm tra mã và đối tượng tra cứu.";return View(vm);}
            vm.Code=person.MaSV;vm.FullName=person.HoTen;vm.DateOfBirth=person.NgaySinh;vm.ClassName=person.MaLop;
            vm.MajorName=person.LopHanhChinh?.Nganh?.TenNganh;vm.FacultyName=person.LopHanhChinh?.Nganh?.Khoa?.TenKhoa;
        }else {
            var person=await db.GiangViens.AsNoTracking().Include(x=>x.Khoa).FirstOrDefaultAsync(x=>x.MaGV==vm.Code);
            if(person==null){vm.Error="Không tìm thấy mã giảng viên. Vui lòng kiểm tra mã và đối tượng tra cứu.";return View(vm);}
            vm.Code=person.MaGV;vm.FullName=person.HoTen;vm.Degree=person.HocVi;vm.Email=person.Email;vm.FacultyName=person.Khoa?.TenKhoa;vm.MajorName=person.Khoa?.TenKhoa;
        }
        vm.HocKys=await db.HocKys.AsNoTracking().OrderByDescending(x=>x.NamHocBatDau).ThenByDescending(x=>x.MaHK).ToListAsync();
        var today=RoomMatrixRules.Today;
        var calendar=RoomMatrixRules.ResolveCalendar(vm.HocKys,maHK,week,null,null,today);
        vm.HocKy=calendar.Term;vm.Week=Math.Clamp(calendar.Week,1,Math.Max(1,calendar.Term?.SoTuan??20));
        vm.NoCurrentTerm=!vm.HocKys.Any(x=>RoomMatrixRules.ContainsDate(x,today));
        if(vm.HocKy!=null) {
            vm.IsHistoricalTerm=TimetablePresentation.IsHistorical(vm.HocKy,today);
            var termId=vm.HocKy.MaHK;
            var q=db.ClassSchedules.AsNoTracking().Include(x=>x.LopHocPhan).ThenInclude(x=>x!.MonHoc).Include(x=>x.GiangVien).Where(x=>x.LopHocPhan!.MaHK==termId);
            if(vm.Kind=="student")q=q.Where(x=>db.KetQuaHocTaps.Any(k=>k.MaSV==vm.Code&&k.MaLHP==x.MaLHP));
            else q=q.Where(x=>x.MaGV==vm.Code);
            vm.TermSchedules=await q.OrderBy(x=>x.Thu).ThenBy(x=>x.TietBatDau).ToListAsync();
            vm.Schedules=vm.Mode=="all"?vm.TermSchedules:vm.TermSchedules.Where(x=>ScheduleRules.Weeks(x.ChuoiTuanHoc).Contains(vm.Week)).ToList();
        }
        vm.LoadedAt=PortalRules.Local(DateTime.UtcNow);
        Response.Headers["Cache-Control"]="no-store";
        return View(vm);
    }
}
