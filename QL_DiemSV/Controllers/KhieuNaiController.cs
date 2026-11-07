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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? maSV, [FromQuery] string? maGV)
    {
        var query = _db.DonKhieuNais.AsNoTracking()
            .Include(d => d.SinhVien)
                .ThenInclude(s => s!.LopHanhChinh)
            .Include(d => d.LopHocPhan)
                .ThenInclude(l => l!.MonHoc)
            .AsQueryable();

        var role = User.FindFirstValue(ClaimTypes.Role);
        var currentMaGV = User.FindFirstValue("MaGV");
        var currentMaSV = User.FindFirstValue("MaSV") ?? (role == "SinhVien" ? User.Identity?.Name : null);

        if (role == "SinhVien" && !string.IsNullOrWhiteSpace(currentMaSV))
        {
            query = query.Where(d => d.MaSV == currentMaSV);
        }
        else if (role == "GiangVien" && !string.IsNullOrWhiteSpace(currentMaGV))
        {
            query = query.Where(d => d.LopHocPhan != null && d.LopHocPhan.MaGV == currentMaGV);
        }
        else if (!string.IsNullOrWhiteSpace(maSV))
        {
            query = query.Where(d => d.MaSV == maSV);
        }
        else if (!string.IsNullOrWhiteSpace(maGV))
        {
            query = query.Where(d => d.LopHocPhan != null && d.LopHocPhan.MaGV == maGV);
        }

        var list = await query.OrderByDescending(d => d.NgayGui).ToListAsync();
        var maSVs = list.Select(d => d.MaSV).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var maLHPs = list.Select(d => d.MaLHP).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

        var kqRelevant = (maSVs.Count > 0 && maLHPs.Count > 0)
            ? await _db.KetQuaHocTaps.AsNoTracking()
                .Where(k => k.MaSV != null && k.MaLHP != null && maSVs.Contains(k.MaSV) && maLHPs.Contains(k.MaLHP))
                .ToListAsync()
            : new List<KetQuaHocTap>();

        var data = list.Select(d =>
        {
            var kq = kqRelevant.FirstOrDefault(k => k.MaSV == d.MaSV && k.MaLHP == d.MaLHP);
            return new
            {
                id = d.MaKN,
                MaKN = d.MaKN,
                studentId = d.MaSV,
                MaSV = d.MaSV,
                studentName = d.SinhVien?.HoTen ?? d.MaSV,
                studentClass = d.SinhVien?.MaLop ?? "N/A",
                maLHP = d.MaLHP,
                courseName = d.LopHocPhan?.MonHoc?.TenMon ?? d.LopHocPhan?.TenLopHP ?? d.MaLHP,
                appealedGrade = d.LoaiDiem,
                LoaiDiem = d.LoaiDiem,
                reason = d.LyDo,
                LyDo = d.LyDo,
                submitDate = d.NgayGui,
                NgayGui = d.NgayGui,
                status = d.TrangThai,
                TrangThai = d.TrangThai,
                feedback = d.PhanHoi,
                PhanHoi = d.PhanHoi,
                DaXem = d.DaXem,
                oldGrades = new
                {
                    quatrinh = kq?.DiemCK,
                    cuoiky = kq?.DiemTongKet,
                    thiLan1 = kq?.DiemThiLan1,
                    thiLan2 = kq?.DiemThiLan2
                }
            };
        });

        return Ok(new { status = "success", data });
    }

    public class CreateKhieuNaiDto
    {
        public string? MaSV { get; set; }
        public string? MaLHP { get; set; }
        public string? LoaiDiem { get; set; }
        public string? LyDo { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateKhieuNaiDto req)
    {
        var maSV = req.MaSV ?? User.FindFirst("MaSV")?.Value ?? User.Identity?.Name ?? "";
        if (string.IsNullOrWhiteSpace(maSV) || string.IsNullOrWhiteSpace(req.MaLHP) || string.IsNullOrWhiteSpace(req.LyDo))
        {
            return BadRequest(new { status = "error", message = "Vui lòng nhập đầy đủ thông tin đơn khiếu nại!" });
        }

        // Kiểm tra có đơn đang chờ xử lý cho cùng môn và loại điểm không
        bool existsPending = await _db.DonKhieuNais.AnyAsync(d =>
            d.MaSV == maSV &&
            d.MaLHP == req.MaLHP &&
            d.LoaiDiem == (req.LoaiDiem ?? "thiLan1") &&
            d.TrangThai == "pending");

        if (existsPending)
        {
            return BadRequest(new { status = "error", message = "Bạn đã có một đơn khiếu nại đang chờ xử lý cho cột điểm này!" });
        }

        var lhp = await _db.LopHocPhans
            .Include(l => l.MonHoc)
            .FirstOrDefaultAsync(l => l.MaLHP == req.MaLHP);

        var sv = await _db.SinhViens.FindAsync(maSV);

        var don = new DonKhieuNai
        {
            MaSV = maSV,
            MaLHP = req.MaLHP,
            LoaiDiem = req.LoaiDiem ?? "thiLan1",
            LyDo = req.LyDo.Trim(),
            NgayGui = DateTime.Now,
            TrangThai = "pending",
            DaXem = false
        };

        _db.DonKhieuNais.Add(don);
        await _db.SaveChangesAsync();

        // Gửi thông báo cho Giảng viên phụ trách lớp học phần
        string recipient = lhp?.MaGV ?? "ADMIN";
        _db.ThongBaos.Add(new ThongBao
        {
            MaNguoiGui = maSV,
            MaNguoiNhan = recipient,
            TieuDe = "Khiếu nại điểm mới",
            NoiDung = $"Sinh viên {sv?.HoTen ?? maSV} ({maSV}) đã gửi đơn phúc khảo điểm môn {lhp?.MonHoc?.TenMon ?? req.MaLHP}.",
            LoaiThongBao = "COMPLAINT_NEW",
            ThamChieuID = don.MaKN.ToString(),
            NgayGui = DateTime.Now,
            DaDoc = false
        });
        await _db.SaveChangesAsync();

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
        var don = await _db.DonKhieuNais
            .Include(d => d.LopHocPhan)
                .ThenInclude(l => l!.MonHoc)
            .FirstOrDefaultAsync(d => d.MaKN == id);

        if (don == null) return NotFound(new { status = "error", message = "Không tìm thấy đơn khiếu nại" });

        var role = User.FindFirstValue(ClaimTypes.Role);
        var currentMaGV = User.FindFirstValue("MaGV");
        if (role == "GiangVien" && !string.IsNullOrWhiteSpace(currentMaGV) && don.LopHocPhan?.MaGV != currentMaGV)
        {
            return StatusCode(403, new { status = "error", message = "Bạn chỉ được xử lý đơn phúc khảo của lớp học phần do mình phụ trách!" });
        }

        if (req.status == "approved")
        {
            if (req.newGrade == null || req.newGrade < 0 || req.newGrade > 10)
            {
                return BadRequest(new { status = "error", message = "Điểm mới phải nằm trong khoảng từ 0 đến 10!" });
            }

            var kq = await _db.KetQuaHocTaps.FirstOrDefaultAsync(k => k.MaSV == don.MaSV && k.MaLHP == don.MaLHP);
            if (kq != null)
            {
                if (don.LoaiDiem == "quatrinh" || don.LoaiDiem == "DiemQT")
                {
                    kq.DiemCK = req.newGrade;
                    // Đồng bộ lại các cột thành phần trong ChiTietDiemQT để khi mở bảng điểm chi tiết không bị tính đè về điểm cũ
                    try
                    {
                        var chiTiet = !string.IsNullOrWhiteSpace(kq.ChiTietDiemQT)
                            ? JsonSerializer.Deserialize<Dictionary<string, double?>>(kq.ChiTietDiemQT) ?? new Dictionary<string, double?>()
                            : new Dictionary<string, double?>();
                        if (chiTiet.Any())
                        {
                            foreach (var k in chiTiet.Keys.ToList()) chiTiet[k] = req.newGrade;
                        }
                        else
                        {
                            chiTiet["col_cc"] = req.newGrade;
                            chiTiet["col_gk"] = req.newGrade;
                        }
                        kq.DiemCC = req.newGrade;
                        kq.DiemGK = req.newGrade;
                        kq.ChiTietDiemQT = JsonSerializer.Serialize(chiTiet);
                    }
                    catch { }
                }
                else if (don.LoaiDiem == "thiLan1" || don.LoaiDiem == "cuoiky" || don.LoaiDiem == "DiemThi" || don.LoaiDiem == "DiemTK") kq.DiemThiLan1 = req.newGrade;
                else if (don.LoaiDiem == "thiLan2") kq.DiemThiLan2 = req.newGrade;

                int tyLeQT = (don.LopHocPhan != null && don.LopHocPhan.TyLeQuaTrinh + don.LopHocPhan.TyLeThiKetThuc == 100)
                    ? don.LopHocPhan.TyLeQuaTrinh : 40;
                int tyLeThi = 100 - tyLeQT;

                var (tk, dc, gc) = GradeHelper.TinhDiemTongKetCuoiCung(kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, tyLeQT, tyLeThi);
                kq.DiemTongKet = tk;
                kq.DiemChu = dc;
                kq.GhiChu = gc;
            }
        }

        don.TrangThai = req.status;
        don.PhanHoi = req.feedback;
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
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Xử lý khiếu nại", $"Đã xử lý đơn khiếu nại #{id} ({req.status}) cho SV {don.MaSV}");
        return Ok(new { status = "success", message = "Đã xử lý đơn khiếu nại thành công!", data = don });
    }
}
