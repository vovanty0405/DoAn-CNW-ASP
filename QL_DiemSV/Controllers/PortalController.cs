using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;
using QL_DiemSV.ViewModels;

namespace QL_DiemSV.Controllers;

[AllowAnonymous, Route("Portal")]
public class PortalController(ApplicationDbContext db) : Controller
{
    [HttpGet("Curriculums")]
    public async Task<IActionResult> Curriculums(string? system,int? year,string? program,string? search) {
      var all=await db.Curriculums.AsNoTracking().Include(x=>x.NganhHoc).Where(x=>x.TrangThai).OrderByDescending(x=>x.KhoaHoc).ToListAsync();
      return View(new CurriculumCatalogViewModel{All=all,System=system,Year=year,Program=program,Search=search,Visible=all.Where(x=>(system==null||x.HeDaoTao==system)&&(!year.HasValue||x.KhoaHoc==year)&&(program==null||x.TenChuongTrinh==program)&&(string.IsNullOrWhiteSpace(search)||(x.NganhHoc?.TenNganh??"").Contains(search,StringComparison.OrdinalIgnoreCase))).ToList()});
    }
    [HttpGet("CurriculumDetail/{id:int}")]
    public async Task<IActionResult> CurriculumDetail(int id) {
      var c=await db.Curriculums.AsNoTracking().Include(x=>x.NganhHoc).Include(x=>x.Sections).FirstOrDefaultAsync(x=>x.Id==id);
      if(c==null||(!c.TrangThai&&!await ContentAccess.AllowsAsync(db,User,ContentAccess.Curriculums,"VIEW")))return NotFound();
      return View(new CurriculumDetailViewModel{Curriculum=c,Alternatives=await db.Curriculums.AsNoTracking().Where(x=>x.TrangThai&&x.NganhHocId==c.NganhHocId&&x.HeDaoTao==c.HeDaoTao).OrderByDescending(x=>x.KhoaHoc).ToListAsync()});
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string category = "all", string? search = null, int page = 1)
    {
        var published = db.Articles.AsNoTracking().Where(x => x.TrangThai == "DaXuatBan");
        var counts = await published
            .GroupBy(x => x.DanhMuc)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var q = published;
        if (PortalRules.Categories.ContainsKey(category))
            q = q.Where(x => x.DanhMuc == category);
        else
            category = "all";

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.TieuDe.Contains(search) || x.TomTat.Contains(search) || x.DonViDang.Contains(search));

        int total = await q.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total + 7) / 8));

        var now = PortalRules.Local(DateTime.UtcNow);
        var date = now.Date;
        var terms = await db.HocKys.AsNoTracking().ToListAsync();
        var current = RoomMatrixRules.ResolveCalendar(terms, null, null, null, null, date);
        var rooms = await db.PhongHocs.AsNoTracking().OrderBy(x => x.MaPhong).Take(8).ToListAsync();
        var lessons = new List<ClassSchedule>();
        if (current.Term != null && !current.OutsideTerm && current.Term.NgayBatDauTuan1.HasValue)
        {
            lessons = (await db.ClassSchedules.AsNoTracking()
                .Where(x => x.LopHocPhan!.MaHK == current.Term.MaHK && x.Thu == current.Day)
                .ToListAsync())
                .Where(x => ScheduleRules.Weeks(x.ChuoiTuanHoc).Contains(current.Week))
                .ToList();
        }

        var active = Enumerable.Range(1, 12).FirstOrDefault(t =>
        {
            var times = RoomMatrixRules.PeriodTime(t).Split(" - ");
            var start = TimeOnly.Parse(times[0]);
            var end = TimeOnly.Parse(times[1]);
            var time = TimeOnly.FromDateTime(now);
            return time >= start && time < end;
        });

        var allPublished = await published
            .OrderByDescending(x => x.GhimDauTrang)
            .ThenByDescending(x => x.NgayDang).ThenByDescending(x => x.Id)
            .Take(80)
            .Select(AcademicQueries.ArticleList)
            .ToListAsync();

        return View(new PortalViewModel
        {
            Category = category,
            Search = search,
            Page = page,
            Total = total,
            Counts = counts,
            Articles = await q
                .OrderByDescending(x => x.GhimDauTrang)
                .ThenByDescending(x => x.NgayDang).ThenByDescending(x => x.Id)
                .Skip((page - 1) * 8)
                .Take(8)
                .Select(AcademicQueries.ArticleList)
            .ToListAsync(),
            AllPublishedArticles = allPublished,
            News = await published
                .Where(x => x.DanhMuc == "TinTuc")
                .OrderByDescending(x => x.NgayDang)
                .Take(3)
                .Select(AcademicQueries.ArticleList)
            .ToListAsync(),
            Curriculums = await db.Curriculums.AsNoTracking().Include(x=>x.NganhHoc).Where(x=>x.TrangThai).OrderByDescending(x=>x.KhoaHoc).ToListAsync(),
            Majors = await db.Nganhs.AsNoTracking()
                .OrderBy(x => x.HeDaoTao)
                .ThenBy(x => x.TenNganh)
                .ToListAsync(),
            Documents = await db.PortalDocuments.AsNoTracking()
                .Where(x => x.HienThi)
                .OrderBy(x => x.TieuDe)
                .ToListAsync(),
            Rooms = rooms.Select(r =>
            {
                var booked = lessons.Where(x => x.PhongHoc == r.MaPhong).ToList();
                return new RoomTodayRow(
                    r.MaPhong,
                    string.Join(", ", booked.Select(x => $"{x.TietBatDau}–{x.TietBatDau + x.SoTiet - 1}")),
                    r.TrangThai != "HoatDong"
                        ? RoomMatrixRules.Statuses.GetValueOrDefault(r.TrangThai, r.TrangThai)
                        : current.Term?.NgayBatDauTuan1 == null || current.OutsideTerm
                            ? "Chưa có lịch"
                            : booked.Any(x => x.TietBatDau <= active && active < x.TietBatDau + x.SoTiet)
                                ? "Đang học"
                                : "Trống");
            }).ToList()
        });
    }

    // Duy nhất MỘT Action cho GET /Portal/Articles
    [HttpGet("Articles")]
    public async Task<IActionResult> Articles(string? category = "all", string? search = null, int page = 1, int pageSize = 9)
    {
        pageSize = pageSize is 6 or 9 or 12 or 18 ? pageSize : 9;
        var published = db.Articles.AsNoTracking().Where(x => x.TrangThai == "DaXuatBan");
        var counts = await published
            .GroupBy(x => x.DanhMuc)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        int totalAll = counts.Values.Sum();

        var q = published;
        if (!string.IsNullOrWhiteSpace(category) && category != "all" && PortalRules.Categories.ContainsKey(category))
            q = q.Where(x => x.DanhMuc == category);
        else
            category = "all";

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            q = q.Where(x => x.TieuDe.Contains(kw) || x.TomTat.Contains(kw) || x.DonViDang.Contains(kw));
        }

        int total = await q.CountAsync();
        int totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);

        var articles = await q
            .OrderByDescending(x => x.GhimDauTrang)
            .ThenByDescending(x => x.NgayDang).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AcademicQueries.ArticleList)
            .ToListAsync();

        return View("Articles", new PortalArticlesViewModel
        {
            Articles = articles,
            Counts = counts,
            Category = category,
            Search = search,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalAll = totalAll
        });
    }

    // Màn hình 3: Trang "Xem chi tiết bài viết" (/Portal/ArticleDetail/{id} và /Portal/bai-viet/{id})
    [HttpGet("ArticleDetail/{id:int}")]
    [HttpGet("bai-viet/{id:int}")]
    public async Task<IActionResult> ArticleDetail(int id)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (article == null || (article.TrangThai != "DaXuatBan" && !await ContentAccess.AllowsAsync(db, User, ContentAccess.Articles, "VIEW")))
            return NotFound();

        var sameCategory = await db.Articles.AsNoTracking()
            .Where(x => x.TrangThai == "DaXuatBan" && x.Id != id && x.DanhMuc == article.DanhMuc)
            .OrderByDescending(x => x.NgayDang)
            .Take(6)
            .Select(AcademicQueries.ArticleList)
            .ToListAsync();

        if (sameCategory.Count < 6)
        {
            var excludeIds = sameCategory.Select(x => x.Id).Append(id).ToList();
            var more = await db.Articles.AsNoTracking()
                .Where(x => x.TrangThai == "DaXuatBan" && !excludeIds.Contains(x.Id))
                .OrderByDescending(x => x.NgayDang)
                .Take(6 - sameCategory.Count)
                .Select(AcademicQueries.ArticleList)
            .ToListAsync();
            sameCategory.AddRange(more);
        }

        return View("ArticleDetail", new PortalArticleDetailViewModel
        {
            Article = article,
            RelatedArticles = sameCategory
        });
    }

    [HttpGet("chuong-trinh/{id}")]
    public async Task<IActionResult> Curriculum(string id)
    {
        var major = await db.Nganhs.AsNoTracking()
            .Include(x => x.Khoa)
            .Include(x => x.KhungChuongTrinhs)
            .ThenInclude(x => x.MonHoc)
            .FirstOrDefaultAsync(x => x.MaNganh == id);
        return major == null ? NotFound() : View(major);
    }

    [HttpGet("tra-cuu-tkb")]
    public async Task<IActionResult> Timetable(string? code, string kind = "student", string? maHK = null, int? week = null, string mode = "week")
    {
        if (string.IsNullOrWhiteSpace(code) && User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("SinhVien"))
            {
                code = User.FindFirstValue("MaSV") ?? User.Identity?.Name;
                kind = "student";
            }
            else if (User.IsInRole("GiangVien"))
            {
                code = User.FindFirstValue("MaGV") ?? User.Identity?.Name;
                kind = "lecturer";
            }
        }

        var vm = new PortalTimetableViewModel
        {
            Code = code?.Trim(),
            Kind = kind == "lecturer" ? "lecturer" : "student",
            Mode = mode == "all" ? "all" : "week"
        };
        if (string.IsNullOrWhiteSpace(vm.Code)) return View(vm);
        if (vm.Code.Length > 20)
        {
            vm.Error = "Mã sinh viên/giảng viên tối đa 20 ký tự.";
            return View(vm);
        }

        if (vm.Kind == "student")
        {
            var person = await db.SinhViens.AsNoTracking()
                .Include(x => x.LopHanhChinh)
                .ThenInclude(x => x!.Nganh)
                .ThenInclude(x => x!.Khoa)
                .FirstOrDefaultAsync(x => x.MaSV == vm.Code);
            if (person == null)
            {
                vm.Error = "Không tìm thấy mã sinh viên. Vui lòng kiểm tra mã và đối tượng tra cứu.";
                return View(vm);
            }
            vm.Code = person.MaSV;
            vm.FullName = person.HoTen;
            vm.DateOfBirth = person.NgaySinh;
            vm.ClassName = person.MaLop;
            vm.MajorName = person.LopHanhChinh?.Nganh?.TenNganh;
            vm.FacultyName = person.LopHanhChinh?.Nganh?.Khoa?.TenKhoa;
        }
        else
        {
            var person = await db.GiangViens.AsNoTracking()
                .Include(x => x.Khoa)
                .FirstOrDefaultAsync(x => x.MaGV == vm.Code);
            if (person == null)
            {
                vm.Error = "Không tìm thấy mã giảng viên. Vui lòng kiểm tra mã và đối tượng tra cứu.";
                return View(vm);
            }
            vm.Code = person.MaGV;
            vm.FullName = person.HoTen;
            vm.Degree = person.HocVi;
            vm.Email = person.Email;
            vm.FacultyName = person.Khoa?.TenKhoa;
            vm.MajorName = person.Khoa?.TenKhoa;
        }

        vm.HocKys = await db.HocKys.AsNoTracking().OrderByDescending(x => x.NamHocBatDau).ThenByDescending(x => x.MaHK).ToListAsync();
        var today = RoomMatrixRules.Today;
        var calendar = RoomMatrixRules.ResolveCalendar(vm.HocKys, maHK, week, null, null, today);
        vm.HocKy = calendar.Term;
        vm.Week = Math.Clamp(calendar.Week, 1, Math.Max(1, calendar.Term?.SoTuan ?? 20));
        vm.NoCurrentTerm = !vm.HocKys.Any(x => RoomMatrixRules.ContainsDate(x, today));
        if (vm.HocKy != null)
        {
            vm.IsHistoricalTerm = TimetablePresentation.IsHistorical(vm.HocKy, today);
            var termId = vm.HocKy.MaHK;
            var q = db.ClassSchedules.AsNoTracking()
                .Where(x => x.LopHocPhan!.MaHK == termId);
            if (vm.Kind == "student")
                q = q.Where(x => db.KetQuaHocTaps.Any(k => k.MaSV == vm.Code && k.MaLHP == x.MaLHP));
            else
                q = q.Where(x => x.MaGV == vm.Code);
            vm.TermSchedules = await q.OrderBy(x => x.Thu).ThenBy(x => x.TietBatDau).Select(AcademicQueries.ScheduleList).ToListAsync();
            vm.Schedules = vm.Mode == "all"
                ? vm.TermSchedules
                : vm.TermSchedules.Where(x => ScheduleRules.Weeks(x.ChuoiTuanHoc).Contains(vm.Week)).ToList();
        }
        vm.LoadedAt = PortalRules.Local(DateTime.UtcNow);
        Response.Headers["Cache-Control"] = "no-store";
        return View(vm);
    }

    [Authorize(Roles = StudentLookupScope.Roles), HttpGet("KetQuaHocTap")]
    public async Task<IActionResult> KetQuaHocTap(string? hocKy = null, string? maSV = null, string? tab = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect("/login");
        }

        var students = await StudentLookupScope.StudentsAsync(db, User);
        var targetMaSV = !User.IsInRole("SinhVien") && !string.IsNullOrWhiteSpace(maSV)
            ? maSV.Trim()
            : await students.OrderBy(s => s.MaSV).Select(s => s.MaSV).FirstOrDefaultAsync();

        string normalizedTab = (tab ?? "").Trim().ToLowerInvariant() switch
        {
            "ho-so" or "hoso" or "profile" => "ho-so",
            "khieu-nai" or "khieunai" or "appeals" => "khieu-nai",
            _ => "ket-qua"
        };

        var vm = new PortalKetQuaHocTapViewModel
        {
            SelectedHocKy = string.IsNullOrWhiteSpace(hocKy) ? "all" : hocKy.Trim(),
            ActiveTab = normalizedTab
        };

        if (string.IsNullOrWhiteSpace(targetMaSV))
        {
            vm.ErrorMessage = "Không xác định được mã sinh viên từ tài khoản đăng nhập.";
            return View(vm);
        }

        var sv = await students
            .Include(s => s.UserAccount)
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.Khoa)
            .Include(s => s.LopHanhChinh)
                .ThenInclude(l => l!.Nganh)
                    .ThenInclude(n => n!.QuyDinhTotNghiep)
            .FirstOrDefaultAsync(s => s.MaSV == targetMaSV);

        if (sv == null)
        {
            vm.ErrorMessage = $"Không tìm thấy hồ sơ sinh viên với mã {targetMaSV}.";
            return View(vm);
        }

        string maNganh = sv.LopHanhChinh?.MaNganh ?? "";
        var quyDinh = sv.LopHanhChinh?.Nganh?.QuyDinhTotNghiep;
        if (quyDinh == null && !string.IsNullOrEmpty(maNganh))
        {
            quyDinh = await db.QuyDinhTotNghieps.AsNoTracking().FirstOrDefaultAsync(q => q.MaNganh == maNganh);
        }

        var khungCT = !string.IsNullOrEmpty(maNganh)
            ? await db.KhungChuongTrinhs.AsNoTracking()
                .Include(k => k.MonHoc)
                .Where(k => k.MaNganh == maNganh)
                .ToListAsync()
            : new List<KhungChuongTrinh>();

        int khungTCBatBuoc = khungCT.Where(k => k.LoaiMon != "Tự chọn" && k.MonHoc != null).Sum(k => k.MonHoc!.SoTinChi);
        int khungTCTuChon = khungCT.Where(k => k.LoaiMon == "Tự chọn" && k.MonHoc != null).Sum(k => k.MonHoc!.SoTinChi);

        int tongTTC = quyDinh != null && quyDinh.TongTTC > 0
            ? quyDinh.TongTTC
            : (khungTCBatBuoc + khungTCTuChon > 0 ? khungTCBatBuoc + khungTCTuChon : 120);
        int tcBatBuoc = quyDinh != null ? quyDinh.TCBatBuoc : khungTCBatBuoc;
        int tcTuChon = quyDinh != null ? quyDinh.TCTuChon : khungTCTuChon;
        double minGPA = quyDinh != null && quyDinh.MinGPA > 0 ? Math.Round(quyDinh.MinGPA, 2) : 2.0;

        // Scalar projection to avoid selecting nvarchar(max) ChiTietDiemQT / CauHinhDiem
        var rawGrades = await db.KetQuaHocTaps.AsNoTracking()
            .Where(k => k.MaSV == targetMaSV && k.LopHocPhan != null && k.LopHocPhan.MonHoc != null && k.LopHocPhan.HocKy != null)
            .Select(k => new
            {
                k.MaKQ,
                k.MaSV,
                k.MaLHP,
                k.DiemCK,
                k.DiemThiLan1,
                k.DiemThiLan2,
                k.DiemTongKet,
                k.DiemChu,
                MaMon = k.LopHocPhan!.MaMon ?? "",
                TenMon = k.LopHocPhan.MonHoc!.TenMon,
                SoTinChi = k.LopHocPhan.MonHoc.SoTinChi,
                MaGV = k.LopHocPhan.MaGV ?? "",
                TenGV = k.LopHocPhan.GiangVien != null ? k.LopHocPhan.GiangVien.HoTen : "Chưa phân công",
                EmailGV = k.LopHocPhan.GiangVien != null ? (k.LopHocPhan.GiangVien.Email ?? "") : "",
                MaHK = k.LopHocPhan.MaHK ?? "",
                TenHK = k.LopHocPhan.HocKy!.TenHK,
                NamHocBatDau = k.LopHocPhan.HocKy.NamHocBatDau,
                NamHocKetThuc = k.LopHocPhan.HocKy.NamHocKetThuc
            })
            .ToListAsync();

        var ketQuas = rawGrades.Select(r => new KetQuaHocTap
        {
            MaKQ = r.MaKQ,
            MaSV = r.MaSV,
            MaLHP = r.MaLHP,
            DiemCK = r.DiemCK,
            DiemThiLan1 = r.DiemThiLan1,
            DiemThiLan2 = r.DiemThiLan2,
            DiemTongKet = r.DiemTongKet,
            DiemChu = r.DiemChu,
            LopHocPhan = new LopHocPhan
            {
                MaLHP = r.MaLHP ?? "",
                MaMon = r.MaMon,
                MaHK = r.MaHK,
                MaGV = r.MaGV,
                GiangVien = new GiangVien
                {
                    MaGV = r.MaGV,
                    HoTen = r.TenGV,
                    Email = r.EmailGV
                },
                MonHoc = new MonHoc
                {
                    MaMon = r.MaMon,
                    TenMon = r.TenMon,
                    SoTinChi = r.SoTinChi
                },
                HocKy = new HocKy
                {
                    MaHK = r.MaHK,
                    TenHK = r.TenHK,
                    NamHocBatDau = r.NamHocBatDau,
                    NamHocKetThuc = r.NamHocKetThuc
                }
            }
        }).ToList();

        var rawComplaints = await db.DonKhieuNais.AsNoTracking()
            .Where(d => d.MaSV == targetMaSV)
            .OrderByDescending(d => d.NgayGui)
            .Select(d => new PortalComplaintItemViewModel
            {
                Id = d.MaKN,
                MaSV = d.MaSV ?? "",
                MaLHP = d.MaLHP ?? "",
                MaMon = d.LopHocPhan != null && d.LopHocPhan.MaMon != null ? d.LopHocPhan.MaMon : "",
                TenMon = d.LopHocPhan != null && d.LopHocPhan.MonHoc != null ? d.LopHocPhan.MonHoc.TenMon : (d.MaLHP ?? ""),
                MaGV = d.LopHocPhan != null && d.LopHocPhan.MaGV != null ? d.LopHocPhan.MaGV : "",
                TenGV = d.LopHocPhan != null && d.LopHocPhan.GiangVien != null ? d.LopHocPhan.GiangVien.HoTen : "Chưa phân công",
                TenHK = d.LopHocPhan != null && d.LopHocPhan.HocKy != null ? d.LopHocPhan.HocKy.TenHK : "",
                NamHoc = d.LopHocPhan != null && d.LopHocPhan.HocKy != null
                    ? (d.LopHocPhan.HocKy.NamHocBatDau + "-" + d.LopHocPhan.HocKy.NamHocKetThuc)
                    : "",
                LoaiDiem = d.LoaiDiem ?? "thiLan1",
                LyDo = d.LyDo ?? "",
                TrangThai = d.TrangThai ?? "pending",
                PhanHoi = d.PhanHoi,
                NgayGui = d.NgayGui
            })
            .ToListAsync();

        var latestComplaintByLhp = rawComplaints
            .GroupBy(c => c.MaLHP)
            .ToDictionary(g => g.Key, g => g.First());

        var khungLookup = khungCT
            .GroupBy(k => k.MaMon)
            .ToDictionary(g => g.Key, g => g.First().LoaiMon != "Tự chọn");

        var cumulativeStats = TranscriptStatistics.Calculate(ketQuas, cumulative: true);

        // Best attempt per course for credit accumulation & failed course checks
        var bestByCourse = ketQuas
            .GroupBy(k => k.LopHocPhan!.MaMon)
            .Select(g => g.OrderByDescending(x => x.DiemTongKet.HasValue)
                .ThenByDescending(x => x.DiemTongKet)
                .ThenByDescending(x => x.MaKQ)
                .First())
            .ToList();

        int tcBatBuocDat = 0;
        int tcTuChonDat = 0;
        int soMonNo = 0;
        int soMonBatBuocNo = 0;

        foreach (var c in bestByCourse)
        {
            string maMon = c.LopHocPhan!.MaMon;
            int soTC = Math.Max(0, c.LopHocPhan.MonHoc!.SoTinChi);
            bool isBatBuoc = !khungLookup.TryGetValue(maMon, out bool bb) || bb;

            if (c.DiemTongKet.HasValue && c.DiemTongKet.Value >= 4.0)
            {
                if (isBatBuoc) tcBatBuocDat += soTC;
                else tcTuChonDat += soTC;
            }
            else if (c.DiemTongKet.HasValue && c.DiemTongKet.Value < 4.0)
            {
                soMonNo++;
                if (isBatBuoc) soMonBatBuocNo++;
            }
        }

        foreach (var k in khungCT.Where(x => x.LoaiMon != "Tự chọn"))
        {
            bool passed = bestByCourse.Any(x => x.LopHocPhan!.MaMon == k.MaMon && x.DiemTongKet >= 4.0);
            bool alreadyCountedFailed = bestByCourse.Any(x => x.LopHocPhan!.MaMon == k.MaMon && x.DiemTongKet.HasValue && x.DiemTongKet.Value < 4.0);
            if (!passed && !alreadyCountedFailed)
            {
                // Course in curriculum not yet completed
                soMonBatBuocNo++;
            }
        }

        int tongTinChiTichLuy = cumulativeStats.TinChiDat;
        double gpaHe4 = cumulativeStats.GPAHe4 ?? 0;

        bool duTongTC = tongTinChiTichLuy >= tongTTC;
        bool duTCBB = tcBatBuocDat >= tcBatBuoc;
        bool duTCTC = tcTuChonDat >= tcTuChon;
        bool duGPA = gpaHe4 >= minGPA;
        bool khongNoMon = soMonNo == 0 && soMonBatBuocNo == 0;

        bool duDieuKien = duTongTC && duTCBB && duTCTC && duGPA && khongNoMon;

        int thieuTC = Math.Max(0, tongTTC - tongTinChiTichLuy);
        string trangThaiText;
        if (duDieuKien)
        {
            trangThaiText = "ĐỦ ĐIỀU KIỆN TỐT NGHIỆP";
        }
        else if (thieuTC > 0)
        {
            trangThaiText = $"CHƯA ĐỦ ĐIỀU KIỆN (Còn thiếu {thieuTC} tín chỉ)";
        }
        else if (!duTCBB)
        {
            trangThaiText = $"CHƯA ĐỦ ĐIỀU KIỆN (Còn thiếu {Math.Max(0, tcBatBuoc - tcBatBuocDat)} TC bắt buộc)";
        }
        else if (!duTCTC)
        {
            trangThaiText = $"CHƯA ĐỦ ĐIỀU KIỆN (Còn thiếu {Math.Max(0, tcTuChon - tcTuChonDat)} TC tự chọn)";
        }
        else if (!duGPA)
        {
            trangThaiText = $"CHƯA ĐỦ ĐIỀU KIỆN (GPA hệ 4 đạt {gpaHe4:F2} / {minGPA:F2})";
        }
        else
        {
            trangThaiText = $"CHƯA ĐỦ ĐIỀU KIỆN (Còn {Math.Max(soMonNo, soMonBatBuocNo)} môn bắt buộc/nợ chưa hoàn thành)";
        }

        var chiTietDieuKien = new List<string>
        {
            duTongTC
                ? $"✓ Đạt tổng tín chỉ tích lũy ({tongTinChiTichLuy}/{tongTTC} TC)"
                : $"✗ Thiếu {thieuTC} tín chỉ tích lũy ({tongTinChiTichLuy}/{tongTTC} TC)",
            duTCBB
                ? $"✓ Đạt tín chỉ bắt buộc ({tcBatBuocDat}/{tcBatBuoc} TC)"
                : $"✗ Thiếu {Math.Max(0, tcBatBuoc - tcBatBuocDat)} tín chỉ bắt buộc ({tcBatBuocDat}/{tcBatBuoc} TC)",
            duTCTC
                ? $"✓ Đạt tín chỉ tự chọn ({tcTuChonDat}/{tcTuChon} TC)"
                : $"✗ Thiếu {Math.Max(0, tcTuChon - tcTuChonDat)} tín chỉ tự chọn ({tcTuChonDat}/{tcTuChon} TC)",
            duGPA
                ? $"✓ GPA tích lũy hệ 4 đạt chuẩn ({gpaHe4:F2} ≥ {minGPA:F2})"
                : $"✗ GPA tích lũy hệ 4 chưa đạt ({gpaHe4:F2} < {minGPA:F2})",
            khongNoMon
                ? "✓ Hoàn thành tất cả học phần bắt buộc và không nợ môn"
                : $"✗ Còn {soMonNo} môn học lại / {soMonBatBuocNo} môn bắt buộc chưa đạt"
        };

        // Group by semester chronologically
        var semesterGroups = ketQuas
            .GroupBy(k => new
            {
                MaHK = k.LopHocPhan!.MaHK ?? "",
                TenHK = k.LopHocPhan.HocKy!.TenHK,
                NamHocBatDau = k.LopHocPhan.HocKy.NamHocBatDau,
                NamHocKetThuc = k.LopHocPhan.HocKy.NamHocKetThuc,
                NamHoc = $"{k.LopHocPhan.HocKy.NamHocBatDau}-{k.LopHocPhan.HocKy.NamHocKetThuc}"
            })
            .OrderBy(g => g.Key.NamHocBatDau)
            .ThenBy(g => TranscriptStatistics.TermOrder(new HocKy { MaHK = g.Key.MaHK, TenHK = g.Key.TenHK, NamHocBatDau = g.Key.NamHocBatDau, NamHocKetThuc = g.Key.NamHocKetThuc }))
            .ThenBy(g => g.Key.MaHK)
            .ToList();

        vm.AvailableSemesters = semesterGroups
            .Select(g => (g.Key.MaHK, g.Key.TenHK, g.Key.NamHoc))
            .ToList();

        var accumulatedCourses = new List<KetQuaHocTap>();
        var allSemesterVms = new List<PortalSemesterGradesViewModel>();

        foreach (var group in semesterGroups)
        {
            var termCourses = group.OrderBy(x => x.LopHocPhan!.MaMon).ToList();
            accumulatedCourses.AddRange(termCourses);

            var hkStats = TranscriptStatistics.Calculate(termCourses, cumulative: false);
            var tlStats = TranscriptStatistics.Calculate(accumulatedCourses, cumulative: true);

            var courseRows = new List<PortalCourseGradeRowViewModel>();
            int stt = 1;
            foreach (var item in termCourses)
            {
                string maMon = item.LopHocPhan!.MaMon;
                bool isBatBuoc = !khungLookup.TryGetValue(maMon, out bool bb) || bb;
                double? diemHe4 = item.DiemTongKet.HasValue ? GradeHelper.QuyDoiHe4(item.DiemTongKet.Value) : null;
                string diemChu = !string.IsNullOrWhiteSpace(item.DiemChu)
                    ? item.DiemChu!
                    : (item.DiemTongKet.HasValue ? GradeHelper.QuyDoiDiemChu(item.DiemTongKet.Value) : "-");
                string ketQua = !item.DiemTongKet.HasValue
                    ? "Chưa có điểm"
                    : (item.DiemTongKet.Value >= 4.0 ? "Đạt" : "Học lại");

                latestComplaintByLhp.TryGetValue(item.MaLHP ?? "", out var latestComplaint);

                courseRows.Add(new PortalCourseGradeRowViewModel
                {
                    STT = stt++,
                    MaKQ = item.MaKQ,
                    MaLHP = item.MaLHP ?? "",
                    MaMon = maMon,
                    TenMon = item.LopHocPhan.MonHoc!.TenMon,
                    SoTC = item.LopHocPhan.MonHoc.SoTinChi,
                    BatBuoc = isBatBuoc,
                    MaGV = item.LopHocPhan.MaGV ?? "",
                    TenGV = item.LopHocPhan.GiangVien?.HoTen ?? "Chưa phân công",
                    EmailGV = item.LopHocPhan.GiangVien?.Email ?? "",
                    DiemQT = item.DiemCK,
                    DiemThiL1 = item.DiemThiLan1,
                    DiemThiL2 = item.DiemThiLan2,
                    DiemTongKet = item.DiemTongKet,
                    DiemHe4 = diemHe4,
                    DiemChu = diemChu,
                    KetQua = ketQua,
                    LatestComplaint = latestComplaint
                });
            }

            allSemesterVms.Add(new PortalSemesterGradesViewModel
            {
                MaHK = group.Key.MaHK,
                TenHK = group.Key.TenHK,
                NamHoc = group.Key.NamHoc,
                GPAHe10HK = hkStats.GPAHe10,
                GPAHe4HK = hkStats.GPAHe4,
                TinChiDangKyHK = hkStats.TinChiDangKy,
                TinChiDatHK = hkStats.TinChiDat,
                XepLoaiHK = hkStats.XepLoai,
                GPAHe10TichLuy = tlStats.GPAHe10,
                GPAHe4TichLuy = tlStats.GPAHe4,
                TinChiTichLuy = tlStats.TinChiDat,
                XepLoaiTichLuy = tlStats.XepLoai,
                Courses = courseRows
            });
        }

        // Display newest semester first or keep filtered semester
        if (!string.Equals(vm.SelectedHocKy, "all", StringComparison.OrdinalIgnoreCase))
        {
            vm.Semesters = allSemesterVms
                .Where(s => string.Equals(s.MaHK, vm.SelectedHocKy, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            vm.Semesters = allSemesterVms.AsEnumerable().Reverse().ToList();
        }

        vm.MaSV = sv.MaSV;
        vm.HoTen = sv.HoTen;
        vm.NgaySinh = sv.NgaySinh;
        vm.GioiTinh = sv.GioiTinh ?? "";
        vm.TenLop = sv.LopHanhChinh?.TenLop ?? sv.MaLop ?? "";
        vm.MaNganh = maNganh;
        vm.TenNganh = sv.LopHanhChinh?.Nganh?.TenNganh ?? "Chưa phân ngành";
        vm.TenKhoa = sv.LopHanhChinh?.Nganh?.Khoa?.TenKhoa ?? "";
        vm.KhoaHoc = sv.LopHanhChinh?.NienKhoa ?? "";
        vm.TrangThaiHocTap = sv.TrangThai ?? "Đang học";
        vm.DiaChi = sv.DiaChi ?? "";
        vm.CCCD = sv.CCCD ?? "";
        vm.Email = sv.Email ?? "";
        vm.SDT = sv.SDT ?? "";
        vm.Username = sv.UserAccount?.Username ?? User.Identity?.Name ?? sv.MaSV;

        vm.TongTTC = tongTTC;
        vm.TCBatBuoc = tcBatBuoc;
        vm.TCTuChon = tcTuChon;
        vm.MinGPA = minGPA;

        vm.TongTinChiTichLuy = tongTinChiTichLuy;
        vm.TCBatBuocDat = tcBatBuocDat;
        vm.TCTuChonDat = tcTuChonDat;
        vm.TongTinChiDangKy = cumulativeStats.TinChiDangKy;
        vm.SoMonNo = soMonNo;
        vm.SoMonBatBuocNo = soMonBatBuocNo;
        vm.GPAHe10 = cumulativeStats.GPAHe10;
        vm.GPAHe4 = cumulativeStats.GPAHe4;
        vm.XepLoai = cumulativeStats.XepLoai;
        vm.DuDieuKienTotNghiep = duDieuKien;
        vm.TrangThaiTotNghiepText = trangThaiText;
        vm.ChiTietDieuKien = chiTietDieuKien;
        vm.Complaints = rawComplaints;

        return View(vm);
    }

    public class UpdateStudentProfileDto
    {
        public string? MaSV { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? CCCD { get; set; }
        public string? SDT { get; set; }
        public string? Email { get; set; }
        public string? DiaChi { get; set; }
    }

    [Authorize(Roles = "Admin,SinhVien"), HttpPut("CapNhatHoSo")]
    public async Task<IActionResult> CapNhatHoSo([FromBody] UpdateStudentProfileDto req)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { status = "error", message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!" });
        }

        string? targetMaSV = User.IsInRole("SinhVien")
            ? (User.FindFirstValue("MaSV") ?? User.Identity?.Name)?.Trim()
            : (req.MaSV ?? User.FindFirstValue("MaSV"))?.Trim();

        if (string.IsNullOrWhiteSpace(targetMaSV))
        {
            return BadRequest(new { status = "error", message = "Không xác định được mã sinh viên cần cập nhật!" });
        }

        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId);
        var sv = await db.SinhViens.FirstOrDefaultAsync(s => s.MaSV == targetMaSV
            && (User.IsInRole("Admin") || s.UserID == accountId));
        if (sv == null)
        {
            return NotFound(new { status = "error", message = "Không tìm thấy hồ sơ sinh viên trong hệ thống!" });
        }

        if (req.NgaySinh.HasValue) sv.NgaySinh = req.NgaySinh.Value;
        if (!string.IsNullOrWhiteSpace(req.GioiTinh)) sv.GioiTinh = req.GioiTinh.Trim();
        sv.CCCD = req.CCCD?.Trim();
        sv.SDT = req.SDT?.Trim();
        sv.Email = req.Email?.Trim();
        sv.DiaChi = req.DiaChi?.Trim();

        await db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(db, User.Identity?.Name ?? targetMaSV, "Cập nhật hồ sơ SV", $"Sinh viên {sv.HoTen} ({sv.MaSV}) đã cập nhật thông tin cá nhân trên Cổng thông tin.");

        return Ok(new { status = "success", message = "Đã cập nhật thông tin hồ sơ sinh viên thành công!" });
    }

    public class ChangeStudentPasswordDto
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }

    [Authorize, HttpPost("DoiMatKhau")]
    public async Task<IActionResult> DoiMatKhau([FromBody] ChangeStudentPasswordDto req)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { status = "error", message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!" });
        }

        var currentPwd = req.CurrentPassword ?? "";
        var newPwd = req.NewPassword ?? "";
        var confirmPwd = req.ConfirmPassword ?? "";

        if (string.IsNullOrWhiteSpace(currentPwd) || string.IsNullOrWhiteSpace(newPwd))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới!" });
        }

        if (newPwd.Length < 6)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu mới phải có tối thiểu 6 ký tự!" });
        }

        if (newPwd != confirmPwd)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu mới và xác nhận mật khẩu không khớp!" });
        }

        var uname = User.Identity?.Name;
        var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Username == uname);
        if (user == null)
        {
            return NotFound(new { status = "error", message = "Không tìm thấy tài khoản đăng nhập!" });
        }

        bool validPassword = false;
        try
        {
            if (user.PasswordHash.StartsWith("$2"))
                validPassword = BCrypt.Net.BCrypt.Verify(currentPwd, user.PasswordHash);
            else
                validPassword = user.PasswordHash == currentPwd;
        }
        catch
        {
            validPassword = user.PasswordHash == currentPwd;
        }

        if (!validPassword)
        {
            return BadRequest(new { status = "error", message = "Mật khẩu hiện tại không chính xác!" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPwd);
        await db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(db, user.Username, "Đổi mật khẩu", $"Người dùng {user.Username} đã đổi mật khẩu trên Cổng thông tin.");

        return Ok(new { status = "success", message = "Đổi mật khẩu tài khoản thành công!" });
    }
}
