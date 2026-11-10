using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.Services;

namespace QL_DiemSV.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class KhieuNaiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public KhieuNaiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [Authorize(Roles = "Admin,GiangVien,KhaoThi,SinhVien"), HttpGet]
    public async Task<IActionResult> GetAll(string? maSV, string? maGV, string? search = null, int page = 1, int pageSize = 10)
    {
        var ct = HttpContext.RequestAborted;
        var query = _db.DonKhieuNais.AsNoTracking();
        if (User.IsInRole("SinhVien"))
        {
            var student = User.FindFirstValue("MaSV");
            query = query.Where(d => !string.IsNullOrEmpty(student) && d.MaSV == student);
        }
        else if (User.IsInRole("GiangVien"))
        {
            var teacher = User.FindFirstValue("MaGV");
            query = query.Where(d => !string.IsNullOrEmpty(teacher) && d.LopHocPhan!.MaGV == teacher);
        }

        if (!string.IsNullOrWhiteSpace(maSV)) query = query.Where(d => d.MaSV == maSV);
        if (!string.IsNullOrWhiteSpace(maGV)) query = query.Where(d => d.LopHocPhan!.MaGV == maGV);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            var number = search.StartsWith("KN-", StringComparison.OrdinalIgnoreCase) ? search[3..] : search;
            query = query.Where(d => d.MaSV.Contains(search) || d.SinhVien!.HoTen.Contains(search) || d.MaKN.ToString().Contains(number));
        }

        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, totalPages);
        var rows = await (
            from d in query.OrderByDescending(d => d.NgayGui).ThenByDescending(d => d.MaKN)
                .Skip((page - 1) * pageSize).Take(pageSize)
            from k in _db.KetQuaHocTaps.AsNoTracking().Where(k => k.MaSV == d.MaSV && k.MaLHP == d.MaLHP)
                .OrderBy(k => k.MaKQ).Take(1).DefaultIfEmpty()
            select new
            {
                d.MaKN, d.MaSV, d.MaLHP, d.LoaiDiem, d.LyDo, d.NgayGui, d.TrangThai, d.PhanHoi, d.DaXem,
                StudentName = d.SinhVien!.HoTen, StudentClass = d.SinhVien.MaLop,
                CourseName = d.LopHocPhan!.MonHoc!.TenMon ?? d.LopHocPhan.TenLopHP,
                quatrinh = k.DiemCK, cuoiky = k.DiemTongKet, thiLan1 = k.DiemThiLan1, thiLan2 = k.DiemThiLan2
            }).ToListAsync(ct);
        var data = rows.Select(d => new
        {
            id = d.MaKN, d.MaKN, studentId = d.MaSV, d.MaSV, studentName = d.StudentName ?? d.MaSV,
            studentClass = d.StudentClass ?? "N/A", maLHP = d.MaLHP, courseName = d.CourseName ?? d.MaLHP,
            appealedGrade = AppealRules.GradeType(d.LoaiDiem) ?? d.LoaiDiem,
            LoaiDiem = AppealRules.GradeType(d.LoaiDiem) ?? d.LoaiDiem, reason = d.LyDo, d.LyDo,
            submitDate = d.NgayGui, d.NgayGui, status = AppealRules.Status(d.TrangThai), TrangThai = AppealRules.Status(d.TrangThai),
            feedback = d.PhanHoi, d.PhanHoi, d.DaXem, oldGrades = new { d.quatrinh, d.cuoiky, d.thiLan1, d.thiLan2 }
        });
        return Ok(new { status = "success", data, page, total, totalPages });
    }

    public class CreateKhieuNaiDto
    {
        public string? MaSV { get; set; }
        public string? MaLHP { get; set; }
        public string? LoaiDiem { get; set; }
        public string? LyDo { get; set; }
    }

    [Authorize(Roles = "Admin,SinhVien"), HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateKhieuNaiDto req)
    {
        var maSV = (User.IsInRole("Admin") ? req.MaSV : User.FindFirstValue("MaSV"))?.Trim();
        var sectionId = req.MaLHP?.Trim();
        var gradeType = AppealRules.GradeType(req.LoaiDiem ?? "thiLan1");
        if (string.IsNullOrEmpty(maSV) || string.IsNullOrEmpty(sectionId)
            || string.IsNullOrWhiteSpace(req.LyDo) || gradeType == null)
            return BadRequest(new { message = "Thông tin đơn phúc khảo hoặc loại điểm không hợp lệ." });

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var results = await _db.KetQuaHocTaps.AsNoTracking()
            .Where(k => k.MaSV == maSV && k.MaLHP == sectionId)
            .Select(k => new { k.DiemCK, k.DiemThiLan1, k.DiemThiLan2, Teacher = k.LopHocPhan!.MaGV,
                Course = k.LopHocPhan.MonHoc!.TenMon, Student = k.SinhVien!.HoTen })
            .Take(2).ToListAsync();
        if (results.Count != 1)
            return BadRequest(new { message = "Không tìm thấy kết quả học tập duy nhất của sinh viên trong lớp này." });
        var result = results[0];
        if (!AppealRules.HasGrade(gradeType, result.DiemCK, result.DiemThiLan1, result.DiemThiLan2))
            return BadRequest(new { message = "Cột điểm được chọn chưa có điểm để phúc khảo." });

        var previous = await _db.DonKhieuNais.AsNoTracking().Where(d => d.MaSV == maSV && d.MaLHP == sectionId)
            .Select(d => new { d.LoaiDiem, d.TrangThai }).ToListAsync();
        if (previous.Any(d => AppealRules.Status(d.TrangThai) == "pending" && AppealRules.GradeType(d.LoaiDiem) == gradeType))
            return Conflict(new { message = "Đã có đơn chờ xử lý cho cột điểm này." });

        var don = new DonKhieuNai
        {
            MaSV = maSV,
            MaLHP = sectionId,
            LoaiDiem = gradeType,
            LyDo = req.LyDo.Trim(),
            NgayGui = DateTime.Now,
            TrangThai = "pending",
            DaXem = false
        };

        _db.DonKhieuNais.Add(don);
        await _db.SaveChangesAsync();

        // Gửi thông báo cho Giảng viên phụ trách lớp học phần
        string recipient = result.Teacher ?? "ADMIN";
        _db.ThongBaos.Add(new ThongBao
        {
            MaNguoiGui = maSV,
            MaNguoiNhan = recipient,
            TieuDe = "Khiếu nại điểm mới",
            NoiDung = $"Sinh viên {result.Student ?? maSV} ({maSV}) đã gửi đơn phúc khảo điểm môn {result.Course ?? sectionId}.",
            LoaiThongBao = "COMPLAINT_NEW",
            ThamChieuID = don.MaKN.ToString(),
            NgayGui = DateTime.Now,
            DaDoc = false
        });
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, maSV, "Gửi đơn khiếu nại", $"Sinh viên {maSV} gửi đơn phúc khảo lớp {req.MaLHP}");
        return Ok(new { status = "success", message = "Đã gửi đơn khiếu nại thành công!", data = don });
    }

    public class ProcessKhieuNaiDto
    {
        public string status { get; set; } = "approved";
        public double? newGrade { get; set; }
        public string? feedback { get; set; }
    }

    [HttpPut("{id}/process")]
    [Authorize(Roles = "Admin,GiangVien,KhaoThi")]
    public async Task<IActionResult> ProcessComplaint(int id, [FromBody] ProcessKhieuNaiDto req)
    {
        if (req.status is not ("approved" or "rejected")
            || (req.status == "rejected" && string.IsNullOrWhiteSpace(req.feedback)))
            return BadRequest(new { message = "Chọn duyệt hoặc từ chối. Khi từ chối cần nhập lý do." });

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var don = await _db.DonKhieuNais.Include(d => d.LopHocPhan).ThenInclude(l => l!.MonHoc)
            .FirstOrDefaultAsync(d => d.MaKN == id);
        if (don == null) return NotFound(new { message = "Không tìm thấy đơn phúc khảo." });

        var currentMaGV = User.FindFirstValue("MaGV");
        if (User.IsInRole("GiangVien") && (string.IsNullOrWhiteSpace(currentMaGV)
            || !string.Equals(don.LopHocPhan?.MaGV, currentMaGV, StringComparison.OrdinalIgnoreCase)))
            return Forbid();
        if (AppealRules.Status(don.TrangThai) != "pending")
            return Conflict(new { message = "Đơn này không còn ở trạng thái chờ xử lý." });

        var gradeType = AppealRules.GradeType(don.LoaiDiem);
        if (req.status == "approved")
        {
            if (req.newGrade == null || !GradeHelper.IsValidGrade(req.newGrade) || gradeType == null)
                return BadRequest(new { message = "Loại điểm phải hợp lệ và điểm mới phải từ 0 đến 10." });

            var section = don.LopHocPhan;
            if (section == null || !GradeHelper.CanEditGradebook(section.TrangThaiNhapDiem))
                return Conflict(new { message = "Cần mở khóa sổ điểm trước khi duyệt thay đổi điểm." });

            var results = await _db.KetQuaHocTaps.Where(k => k.MaSV == don.MaSV && k.MaLHP == don.MaLHP)
                .Take(2).ToListAsync();
            if (results.Count != 1 || !AppealRules.HasGrade(gradeType, results[0].DiemCK, results[0].DiemThiLan1, results[0].DiemThiLan2))
                return Conflict(new { message = "Không xác định được cột điểm hiện tại để cập nhật." });

            var kq = results[0];
            switch (gradeType)
            {
                case "quatrinh":
                    Dictionary<string, double?> details;
                    try
                    {
                        details = string.IsNullOrWhiteSpace(kq.ChiTietDiemQT) ? new()
                            : JsonSerializer.Deserialize<Dictionary<string, double?>>(kq.ChiTietDiemQT) ?? new();
                    }
                    catch (JsonException)
                    {
                        return Conflict(new { message = "Chi tiết điểm quá trình không hợp lệ. Cần kiểm tra sổ điểm trước khi duyệt." });
                    }
                    if (kq.DiemCC != null) details.TryAdd("col_cc", kq.DiemCC);
                    if (kq.DiemGK != null) details.TryAdd("col_gk", kq.DiemGK);
                    details[GradeHelper.CourseworkAppealKey] = req.newGrade;
                    kq.ChiTietDiemQT = JsonSerializer.Serialize(details);
                    kq.DiemCK = req.newGrade;
                    break;
                case "thiLan1": kq.DiemThiLan1 = req.newGrade; break;
                case "thiLan2": kq.DiemThiLan2 = req.newGrade; break;
            }

            int tyLeQT = section.TyLeQuaTrinh is >= 0 and <= 100 && section.TyLeQuaTrinh + section.TyLeThiKetThuc == 100
                ? section.TyLeQuaTrinh : 40;
            (kq.DiemTongKet, kq.DiemChu, kq.GhiChu) = GradeHelper.TinhDiemTongKetCuoiCung(
                kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, tyLeQT, 100 - tyLeQT);
        }

        if (gradeType != null) don.LoaiDiem = gradeType;
        don.TrangThai = req.status;
        don.PhanHoi = req.feedback?.Trim();
        don.DaXem = true;

        // Gửi thông báo cho Sinh viên
        string statusText = req.status == "approved" ? "Chấp nhận (Đã cập nhật điểm)" : "Từ chối";
        string tenMon = don.LopHocPhan?.MonHoc?.TenMon ?? don.MaLHP;
        string noiDungPhanHoi = !string.IsNullOrWhiteSpace(req.feedback) ? req.feedback.Trim() : statusText;
        _db.ThongBaos.Add(new ThongBao
        {
            MaNguoiGui = don.LopHocPhan?.MaGV ?? User.Identity?.Name ?? "GV",
            MaNguoiNhan = don.MaSV,
            TieuDe = $"Phản hồi đơn khiếu nại điểm môn {tenMon}",
            NoiDung = $"Giảng viên đã phản hồi đơn khiếu nại của bạn: {noiDungPhanHoi}",
            LoaiThongBao = "PhucKhaoDiem",
            ThamChieuID = don.MaKN.ToString(),
            NgayGui = DateTime.Now,
            DaDoc = false
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xử lý khiếu nại", $"Đã xử lý đơn khiếu nại #{id} ({req.status}) cho SV {don.MaSV}");
        return Ok(new { status = "success", message = "Đã xử lý đơn khiếu nại thành công!", data = don });
    }
}
