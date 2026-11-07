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
[Authorize(Roles = "Admin,GiangVien,KhaoThi")]
public class DiemController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DiemController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private string CurrentMaGV => User.FindFirstValue("MaGV") ?? "";

    private bool CanManageSection(LopHocPhan lhp)
    {
        if (CurrentRole == "Admin" || CurrentRole == "KhaoThi") return true;
        if (CurrentRole == "GiangVien" && !string.IsNullOrWhiteSpace(CurrentMaGV))
            return string.Equals(lhp.MaGV, CurrentMaGV, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public class ColConfigItem
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public double weight { get; set; }
    }

    private List<ColConfigItem> ParseCauHinhDiem(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ColConfigItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null && parsed.Any()) return parsed;
            }
            catch { }
        }
        return new List<ColConfigItem>
        {
            new ColConfigItem { id = "col_cc", name = "Chuyên cần", weight = 25 },
            new ColConfigItem { id = "col_gk", name = "Giữa kỳ", weight = 75 }
        };
    }

    private Dictionary<string, double?> ParseChiTietQT(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, double?>>(json);
                if (parsed != null) return parsed;
            }
            catch { }
        }
        return new Dictionary<string, double?>();
    }

    private static (int tyLeQT, int tyLeThi) GetValidTyLe(LopHocPhan lhp)
    {
        if (lhp.TyLeQuaTrinh >= 0 && lhp.TyLeThiKetThuc >= 0 && lhp.TyLeQuaTrinh + lhp.TyLeThiKetThuc == 100)
        {
            return (lhp.TyLeQuaTrinh, lhp.TyLeThiKetThuc);
        }
        return (40, 60);
    }

    private async Task<bool> VerifyUserPasswordAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        var uname = User.Identity?.Name ?? "admin";
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Username == uname);
        if (currentUser == null) return false;

        try
        {
            return currentUser.PasswordHash.StartsWith("$2")
                ? BCrypt.Net.BCrypt.Verify(password, currentUser.PasswordHash)
                : currentUser.PasswordHash == password;
        }
        catch
        {
            return false;
        }
    }

    [HttpGet("lophocphan/{maLHP}")]
    public async Task<IActionResult> GetBangDiemLopHocPhan(string maLHP)
    {
        var lhp = await _db.LopHocPhans.AsNoTracking()
            .Include(l => l.MonHoc)
                .ThenInclude(m => m!.Khoa)
            .Include(l => l.GiangVien)
            .Include(l => l.HocKy)
            .FirstOrDefaultAsync(l => l.MaLHP == maLHP);

        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        var kqList = (await _db.KetQuaHocTaps.AsNoTracking()
            .Where(k => k.MaLHP == maLHP)
            .Select(k => new
            {
                k.MaKQ,
                k.MaSV,
                k.MaLHP,
                k.DiemCC,
                k.DiemGK,
                k.DiemCK,
                k.DiemThiLan1,
                k.DiemThiLan2,
                k.DiemTongKet,
                k.DiemChu,
                k.GhiChu,
                k.ChiTietDiemQT,
                SinhVien = k.SinhVien == null ? null : new
                {
                    k.SinhVien.MaSV,
                    k.SinhVien.HoTen,
                    k.SinhVien.MaLop,
                    LopHanhChinh = k.SinhVien.LopHanhChinh == null ? null : new
                    {
                        k.SinhVien.LopHanhChinh.MaLop,
                        k.SinhVien.LopHanhChinh.TenLop
                    }
                }
            })
            .ToListAsync())
            .OrderBy(k => k.MaSV)
            .ToList();

        var cauHinh = ParseCauHinhDiem(lhp.CauHinhDiem);
        var (tyLeQT, tyLeThi) = GetValidTyLe(lhp);

        var danhSachDiem = kqList.Select(k =>
        {
            var chiTiet = ParseChiTietQT(k.ChiTietDiemQT);
            if (!chiTiet.Any())
            {
                if (k.DiemCC != null) chiTiet["col_cc"] = k.DiemCC;
                if (k.DiemGK != null) chiTiet["col_gk"] = k.DiemGK;
            }
            return new
            {
                k.MaKQ,
                k.MaSV,
                k.MaLHP,
                k.DiemCC,
                k.DiemGK,
                k.DiemCK,
                k.DiemThiLan1,
                k.DiemThiLan2,
                k.DiemTongKet,
                k.DiemChu,
                k.GhiChu,
                ChiTietDiemQT = chiTiet,
                k.SinhVien
            };
        }).ToList();

        return Ok(new
        {
            status = "success",
            data = new
            {
                thongTinLHP = new
                {
                    lhp.MaLHP,
                    lhp.TenLopHP,
                    lhp.MaMon,
                    lhp.MaGV,
                    lhp.MaHK,
                    lhp.PhongHoc,
                    NMH = string.IsNullOrWhiteSpace(lhp.NMH) ? "01" : lhp.NMH,
                    STC = lhp.STC > 0 ? lhp.STC : (lhp.MonHoc?.SoTinChi ?? 3),
                    SiSoHienTai = kqList.Count,
                    SoSVDaNhapDiem = kqList.Count(k => k.DiemTongKet != null),
                    lhp.TrangThai,
                    lhp.TrangThaiNhapDiem,
                    TyLeQuaTrinh = tyLeQT,
                    TyLeThiKetThuc = tyLeThi,
                    CauHinhDiem = cauHinh,
                    lhp.MonHoc,
                    lhp.GiangVien,
                    lhp.HocKy
                },
                danhSachDiem
            }
        });
    }

    public class TyLeDiemHocPhanDto
    {
        public int tyLeQuaTrinh { get; set; } = 40;
        public int tyLeThiKetThuc { get; set; } = 60;
    }

    [HttpPut("lophocphan/{maLHP}/ty-le")]
    public async Task<IActionResult> UpdateTyLeDiemHocPhan(string maLHP, [FromBody] TyLeDiemHocPhanDto req)
    {
        var lhp = await _db.LopHocPhans.FindAsync(maLHP);
        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (!CanManageSection(lhp))
            return StatusCode(403, new { status = "error", message = "Bạn không có quyền thay đổi cấu hình tỷ lệ của lớp học phần này!" });

        if (lhp.TrangThaiNhapDiem == "Đã khóa")
            return BadRequest(new { status = "error", message = "Bảng điểm đã khóa, vui lòng mở khóa sổ trước khi thay đổi cấu hình tỷ lệ điểm học phần!" });

        if (req.tyLeQuaTrinh < 0 || req.tyLeThiKetThuc < 0 || req.tyLeQuaTrinh + req.tyLeThiKetThuc != 100)
            return BadRequest(new { status = "error", message = $"Tổng tỷ lệ Điểm Quá trình ({req.tyLeQuaTrinh}%) và Điểm Thi Kết thúc ({req.tyLeThiKetThuc}%) phải bằng 100%!" });

        lhp.TyLeQuaTrinh = req.tyLeQuaTrinh;
        lhp.TyLeThiKetThuc = req.tyLeThiKetThuc;

        var kqList = await _db.KetQuaHocTaps.Where(k => k.MaLHP == maLHP).ToListAsync();
        foreach (var kq in kqList)
        {
            var (tk, dc, gc) = GradeHelper.TinhDiemTongKetCuoiCung(
                kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, lhp.TyLeQuaTrinh, lhp.TyLeThiKetThuc);
            kq.DiemTongKet = tk;
            kq.DiemChu = dc;
            kq.GhiChu = gc;
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cấu hình tỷ lệ học phần",
            $"Cập nhật tỷ lệ điểm học phần lớp {maLHP}: Quá trình {lhp.TyLeQuaTrinh}% - Thi kết thúc {lhp.TyLeThiKetThuc}%");

        return Ok(new
        {
            status = "success",
            message = $"Đã cập nhật tỷ lệ điểm học phần: Quá trình {lhp.TyLeQuaTrinh}% - Thi {lhp.TyLeThiKetThuc}%",
            data = new { lhp.TyLeQuaTrinh, lhp.TyLeThiKetThuc }
        });
    }

    public class CauHinhDto
    {
        public List<ColConfigItem> cauHinhDiem { get; set; } = new();
    }

    [HttpPut("lophocphan/{maLHP}/cau-hinh")]
    public async Task<IActionResult> UpdateCauHinhDiem(string maLHP, [FromBody] CauHinhDto req)
    {
        var lhp = await _db.LopHocPhans.FindAsync(maLHP);
        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (!CanManageSection(lhp))
            return StatusCode(403, new { status = "error", message = "Bạn không có quyền thay đổi cấu hình cột điểm của lớp học phần này!" });

        if (lhp.TrangThaiNhapDiem == "Đã khóa")
            return BadRequest(new { status = "error", message = "Bảng điểm đã khóa, vui lòng mở khóa sổ trước khi thay đổi cấu hình cột điểm!" });

        if (req.cauHinhDiem == null || !req.cauHinhDiem.Any())
            return BadRequest(new { status = "error", message = "Cấu hình cột điểm không hợp lệ" });

        double totalWeight = req.cauHinhDiem.Sum(c => c.weight);
        if (Math.Abs(totalWeight - 100) > 0.01)
            return BadRequest(new { status = "error", message = $"Tổng trọng số các cột điểm quá trình phải bằng 100% (Hiện tại: {totalWeight}%)" });

        lhp.CauHinhDiem = JsonSerializer.Serialize(req.cauHinhDiem);
        var (tyLeQT, tyLeThi) = GetValidTyLe(lhp);

        // Tính lại điểm quá trình tổng (DiemCK) và điểm tổng kết cho các sinh viên trong lớp
        var kqList = await _db.KetQuaHocTaps.Where(k => k.MaLHP == maLHP).ToListAsync();
        foreach (var kq in kqList)
        {
            var chiTiet = ParseChiTietQT(kq.ChiTietDiemQT);
            bool hasAllCols = req.cauHinhDiem.All(c => chiTiet.ContainsKey(c.id) && chiTiet[c.id] != null);
            if (hasAllCols)
            {
                double qtSum = req.cauHinhDiem.Sum(c => (chiTiet[c.id] ?? 0) * (c.weight / 100.0));
                kq.DiemCK = Math.Round(qtSum, 1, MidpointRounding.AwayFromZero);
                var (tk, dc, gc) = GradeHelper.TinhDiemTongKetCuoiCung(kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, tyLeQT, tyLeThi);
                kq.DiemTongKet = tk;
                kq.DiemChu = dc;
                kq.GhiChu = gc;
            }
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cấu hình cột điểm", $"Cập nhật cấu hình cột điểm quá trình cho lớp {maLHP}");
        return Ok(new { status = "success", message = "Đã lưu cấu hình cột điểm thành công", data = req.cauHinhDiem });
    }

    public class DiemRowItemDto
    {
        public string? MaSV { get; set; }
        public double? DiemCC { get; set; }
        public double? DiemGK { get; set; }
        public double? DiemCK { get; set; }
        public double? DiemThiLan1 { get; set; }
        public double? DiemThiLan2 { get; set; }
        public Dictionary<string, double?>? ChiTietDiemQT { get; set; }
    }

    public class SaveBangDiemDto
    {
        public List<DiemRowItemDto> danhSachDiem { get; set; } = new();
        public string? password { get; set; }
        public int? tyLeQuaTrinh { get; set; }
        public int? tyLeThiKetThuc { get; set; }
    }

    [HttpPut("lophocphan/{maLHP}/hang-loat")]
    public async Task<IActionResult> SaveBangDiemHangLoat(string maLHP, [FromBody] SaveBangDiemDto req)
    {
        var lhp = await _db.LopHocPhans.FindAsync(maLHP);
        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (!CanManageSection(lhp))
        {
            return StatusCode(403, new { status = "error", message = "Bạn không có quyền nhập điểm cho lớp học phần này!" });
        }

        // Bắt buộc xác thực mật khẩu khi Lưu Bảng Điểm
        if (!await VerifyUserPasswordAsync(req.password))
        {
            return BadRequest(new { status = "error", message = "Mật khẩu xác nhận không chính xác!" });
        }

        if (lhp.TrangThaiNhapDiem == "Đã khóa")
        {
            return BadRequest(new { status = "error", message = "Bảng điểm của lớp này đã bị khóa! Vui lòng mở khóa sổ trước khi chỉnh sửa." });
        }

        if (req.tyLeQuaTrinh != null && req.tyLeThiKetThuc != null &&
            req.tyLeQuaTrinh >= 0 && req.tyLeThiKetThuc >= 0 &&
            req.tyLeQuaTrinh.Value + req.tyLeThiKetThuc.Value == 100)
        {
            lhp.TyLeQuaTrinh = req.tyLeQuaTrinh.Value;
            lhp.TyLeThiKetThuc = req.tyLeThiKetThuc.Value;
        }

        var (tyLeQT, tyLeThi) = GetValidTyLe(lhp);
        var cauHinh = ParseCauHinhDiem(lhp.CauHinhDiem);
        var kqRecords = await _db.KetQuaHocTaps.Where(k => k.MaLHP == maLHP).ToListAsync();

        foreach (var item in req.danhSachDiem)
        {
            if (string.IsNullOrWhiteSpace(item.MaSV)) continue;
            var kq = kqRecords.FirstOrDefault(k => k.MaSV == item.MaSV);
            if (kq == null) continue;

            var chiTiet = ParseChiTietQT(kq.ChiTietDiemQT);
            if (item.ChiTietDiemQT != null)
            {
                foreach (var kv in item.ChiTietDiemQT)
                {
                    chiTiet[kv.Key] = kv.Value;
                }
            }

            if (item.DiemCC != null) { kq.DiemCC = item.DiemCC; chiTiet["col_cc"] = item.DiemCC; }
            else if (chiTiet.TryGetValue("col_cc", out var ccVal)) { kq.DiemCC = ccVal; }

            if (item.DiemGK != null) { kq.DiemGK = item.DiemGK; chiTiet["col_gk"] = item.DiemGK; }
            else if (chiTiet.TryGetValue("col_gk", out var gkVal)) { kq.DiemGK = gkVal; }

            kq.ChiTietDiemQT = JsonSerializer.Serialize(chiTiet);

            // Tính điểm quá trình tổng (DiemCK) từ các cột động nếu có đủ hoặc từ DiemCK gửi lên
            bool hasAllCols = cauHinh.All(c => chiTiet.ContainsKey(c.id) && chiTiet[c.id] != null);
            if (hasAllCols)
            {
                double qtSum = cauHinh.Sum(c => (chiTiet[c.id] ?? 0) * (c.weight / 100.0));
                kq.DiemCK = Math.Round(qtSum, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                kq.DiemCK = item.DiemCK;
            }

            kq.DiemThiLan1 = item.DiemThiLan1;
            kq.DiemThiLan2 = item.DiemThiLan2;

            var (tk, dc, gc) = GradeHelper.TinhDiemTongKetCuoiCung(kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, tyLeQT, tyLeThi);
            kq.DiemTongKet = tk;
            kq.DiemChu = dc;
            kq.GhiChu = gc;
        }

        if (string.IsNullOrWhiteSpace(lhp.TrangThaiNhapDiem) ||
            lhp.TrangThaiNhapDiem == "Vui lòng nhập điểm" ||
            lhp.TrangThaiNhapDiem == "Chưa nhập")
        {
            lhp.TrangThaiNhapDiem = "Đang nhập";
        }

        // Gửi thông báo tự động cho toàn bộ sinh viên trong lớp học phần khi cập nhật bảng điểm
        var tenMon = await _db.MonHocs.AsNoTracking()
            .Where(m => m.MaMon == lhp.MaMon)
            .Select(m => m.TenMon)
            .FirstOrDefaultAsync() ?? lhp.TenLopHP ?? maLHP;
        var senderCode = lhp.MaGV ?? User.Identity?.Name ?? "ADMIN";
        var now = DateTime.Now;
        var studentIds = kqRecords
            .Select(k => k.MaSV)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        foreach (var studentId in studentIds)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                MaNguoiGui = senderCode,
                MaNguoiNhan = studentId!,
                TieuDe = $"Đã cập nhật bảng điểm môn {tenMon} ({maLHP})",
                NoiDung = $"Bảng điểm học phần {tenMon} đã được công bố/cập nhật. Vui lòng vào Cổng thông tin để kiểm tra kết quả học tập.",
                LoaiThongBao = "DiemSo",
                ThamChieuID = maLHP,
                NgayGui = now,
                DaDoc = false
            });
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Cập nhật bảng điểm", $"Đã lưu điểm cho {req.danhSachDiem.Count} sinh viên tại lớp {maLHP} (Tỷ lệ QT {tyLeQT}% - Thi {tyLeThi}%)");
        return Ok(new { status = "success", message = "Đã cập nhật bảng điểm thành công!" });
    }

    public class TrangThaiDto
    {
        public string? TrangThaiNhapDiem { get; set; }
        public string? password { get; set; }
    }

    [HttpPut("lophocphan/{maLHP}/trang-thai")]
    public async Task<IActionResult> UpdateTrangThaiNhapDiem(string maLHP, [FromBody] TrangThaiDto req)
    {
        var lhp = await _db.LopHocPhans
            .Include(l => l.MonHoc)
            .FirstOrDefaultAsync(l => l.MaLHP == maLHP);

        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        string newStatus = req.TrangThaiNhapDiem ?? "Đã khóa";

        if (!CanManageSection(lhp))
        {
            return StatusCode(403, new { status = "error", message = "Bạn không có quyền thay đổi trạng thái sổ điểm của lớp học phần này!" });
        }

        if (newStatus == "Đang nhập" && CurrentRole != "Admin" && CurrentRole != "KhaoThi")
        {
            return StatusCode(403, new { status = "error", message = "Chỉ Quản trị viên hoặc Phòng Khảo thí mới có quyền Mở khóa sổ điểm!" });
        }

        // Khi Khóa sổ điểm hoặc Admin Mở khóa sổ điểm thì bắt buộc xác thực mật khẩu
        if (newStatus == "Đã khóa" || newStatus == "Đang nhập")
        {
            if (!await VerifyUserPasswordAsync(req.password))
            {
                return BadRequest(new { status = "error", message = "Mật khẩu xác nhận không chính xác!" });
            }
        }

        lhp.TrangThaiNhapDiem = newStatus;

        var uname = User.Identity?.Name ?? "GV";
        if (newStatus == "Chờ mở khóa")
        {
            _db.ThongBaos.Add(new ThongBao
            {
                MaNguoiGui = lhp.MaGV ?? uname,
                MaNguoiNhan = "ADMIN",
                TieuDe = $"Yêu cầu mở khóa điểm {maLHP}",
                NoiDung = $"Giảng viên {lhp.MaGV ?? uname} yêu cầu mở khóa bảng điểm cho lớp {lhp.TenLopHP} ({maLHP}).",
                LoaiThongBao = "UNLOCK_REQUEST",
                ThamChieuID = maLHP,
                NgayGui = DateTime.Now,
                DaDoc = false
            });
            GradeHelper.GhiNhatKy(_db, uname, "Xin mở khóa điểm", $"Yêu cầu mở khóa điểm lớp {maLHP}", "Cảnh báo");
        }
        else if (newStatus == "Đang nhập")
        {
            if (!string.IsNullOrEmpty(lhp.MaGV))
            {
                _db.ThongBaos.Add(new ThongBao
                {
                    MaNguoiGui = "ADMIN",
                    MaNguoiNhan = lhp.MaGV,
                    TieuDe = $"Đã mở khóa điểm lớp {maLHP}",
                    NoiDung = $"Quản trị viên đã phê duyệt mở khóa bảng điểm cho lớp {lhp.TenLopHP} ({maLHP}).",
                    LoaiThongBao = "UNLOCK_APPROVED",
                    ThamChieuID = maLHP,
                    NgayGui = DateTime.Now,
                    DaDoc = false
                });
            }
            GradeHelper.GhiNhatKy(_db, uname, "Mở khóa điểm", $"Đã mở khóa bảng điểm lớp {maLHP}");
        }
        else if (newStatus == "Đã khóa")
        {
            var tenMonLock = lhp.MonHoc?.TenMon ?? lhp.TenLopHP ?? maLHP;
            var senderLock = lhp.MaGV ?? uname;
            var nowLock = DateTime.Now;
            var svIds = await _db.KetQuaHocTaps.AsNoTracking()
                .Where(k => k.MaLHP == maLHP && k.MaSV != null)
                .Select(k => k.MaSV!)
                .Distinct()
                .ToListAsync();

            foreach (var svId in svIds)
            {
                _db.ThongBaos.Add(new ThongBao
                {
                    MaNguoiGui = senderLock,
                    MaNguoiNhan = svId,
                    TieuDe = $"Đã cập nhật bảng điểm môn {tenMonLock} ({maLHP})",
                    NoiDung = $"Bảng điểm học phần {tenMonLock} đã được công bố/cập nhật. Vui lòng vào Cổng thông tin để kiểm tra kết quả học tập.",
                    LoaiThongBao = "DiemSo",
                    ThamChieuID = maLHP,
                    NgayGui = nowLock,
                    DaDoc = false
                });
            }

            GradeHelper.GhiNhatKy(_db, uname, "Chốt sổ điểm", $"Đã khóa bảng điểm lớp {maLHP}");
        }

        await _db.SaveChangesAsync();
        return Ok(new { status = "success", message = $"Đã chuyển trạng thái bảng điểm sang: {newStatus}", data = lhp });
    }

    public class AutoSeedGradesDto
    {
        public bool overwriteExisting { get; set; } = false;
        public string? maHK { get; set; }
    }

    [HttpPost("lophocphan/{maLHP}/auto-seed")]
    public async Task<IActionResult> AutoSeedGradesForSection(string maLHP, [FromBody] AutoSeedGradesDto? req = null)
    {
        var lhp = await _db.LopHocPhans.FirstOrDefaultAsync(l => l.MaLHP == maLHP);
        if (lhp == null) return NotFound(new { status = "error", message = "Không tìm thấy Lớp học phần" });

        if (!CanManageSection(lhp))
            return StatusCode(403, new { status = "error", message = "Bạn không có quyền tạo điểm mẫu cho lớp học phần này!" });

        if (lhp.TrangThaiNhapDiem == "Đã khóa")
            return BadRequest(new { status = "error", message = "Bảng điểm của lớp này đang bị khóa! Vui lòng mở khóa sổ trước khi tạo điểm mẫu." });

        bool overwrite = req?.overwriteExisting ?? false;
        int updatedCount = await SeedGradesForSingleSectionAsync(lhp, overwrite);
        await _db.SaveChangesAsync();

        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Tạo điểm mẫu tự động", $"Đã sinh điểm mẫu cho {updatedCount} sinh viên tại lớp {maLHP}");
        return Ok(new
        {
            status = "success",
            message = $"Đã tạo điểm mẫu hợp lệ cho {updatedCount} sinh viên lớp {maLHP}!",
            updatedCount
        });
    }

    [HttpPost("auto-seed-all")]
    [Authorize(Roles = "Admin,KhaoThi")]
    public async Task<IActionResult> AutoSeedAllSections([FromBody] AutoSeedGradesDto? req = null)
    {
        bool overwrite = req?.overwriteExisting ?? false;
        var query = _db.LopHocPhans.AsQueryable();
        if (!string.IsNullOrWhiteSpace(req?.maHK))
        {
            query = query.Where(l => l.MaHK == req.maHK);
        }

        var openSections = await query
            .Where(l => l.TrangThaiNhapDiem != "Đã khóa")
            .ToListAsync();

        int sectionCount = 0;
        int studentCount = 0;

        foreach (var lhp in openSections)
        {
            int c = await SeedGradesForSingleSectionAsync(lhp, overwrite);
            if (c > 0)
            {
                sectionCount++;
                studentCount += c;
            }
        }

        await _db.SaveChangesAsync();
        GradeHelper.GhiNhatKy(_db, User.Identity?.Name, "Tạo điểm mẫu hàng loạt", $"Đã sinh điểm mẫu cho {studentCount} lượt sinh viên trên {sectionCount} lớp học phần");
        return Ok(new
        {
            status = "success",
            message = $"Đã tạo điểm mẫu thành công cho {studentCount} sinh viên thuộc {sectionCount} lớp học phần!",
            sectionCount,
            studentCount
        });
    }

    private async Task<int> SeedGradesForSingleSectionAsync(LopHocPhan lhp, bool overwriteExisting)
    {
        var cauHinh = ParseCauHinhDiem(lhp.CauHinhDiem);
        var (tyLeQT, tyLeThi) = GetValidTyLe(lhp);
        var kqList = (await _db.KetQuaHocTaps.Where(k => k.MaLHP == lhp.MaLHP).ToListAsync())
            .OrderBy(k => k.MaSV)
            .ToList();
        if (!kqList.Any()) return 0;

        int updated = 0;
        for (int idx = 0; idx < kqList.Count; idx++)
        {
            var kq = kqList[idx];
            if (!overwriteExisting && kq.DiemTongKet != null)
                continue;

            // Tạo seed xác định nhưng phân phối chuẩn thực tế dựa trên mã SV + mã LHP
            int hash = Math.Abs(HashCode.Combine(kq.MaSV ?? "", lhp.MaLHP, idx));
            var rng = new Random(hash);

            // Phân phối năng lực sinh viên:
            // 18% Xuất sắc (8.5 - 9.8), 32% Giỏi (7.2 - 8.4), 32% Khá (5.8 - 7.1), 10% TB (4.2 - 5.7), 8% Thi lại / Học lại (2.0 - 3.8)
            int bucket = hash % 100;
            double baseMin, baseMax;
            if (bucket < 18) { baseMin = 8.5; baseMax = 9.8; }
            else if (bucket < 50) { baseMin = 7.2; baseMax = 8.4; }
            else if (bucket < 82) { baseMin = 5.8; baseMax = 7.1; }
            else if (bucket < 92) { baseMin = 4.3; baseMax = 5.7; }
            else { baseMin = 2.0; baseMax = 3.8; }

            double SampleScore(double min, double max)
            {
                double val = min + rng.NextDouble() * (max - min);
                return Math.Clamp(Math.Round(val, 1, MidpointRounding.AwayFromZero), 0.0, 10.0);
            }

            var chiTiet = new Dictionary<string, double?>();
            foreach (var col in cauHinh)
            {
                // Điểm chuyên cần thường cao hơn một chút (tối thiểu 6.0)
                double cMin = col.id.Contains("cc", StringComparison.OrdinalIgnoreCase) ? Math.Max(6.5, baseMin) : Math.Max(3.5, baseMin - 0.3);
                double cMax = col.id.Contains("cc", StringComparison.OrdinalIgnoreCase) ? Math.Min(10.0, baseMax + 0.5) : Math.Min(10.0, baseMax + 0.4);
                chiTiet[col.id] = SampleScore(cMin, cMax);
            }

            kq.DiemCC = chiTiet.TryGetValue("col_cc", out var ccVal) ? ccVal : SampleScore(Math.Max(6.5, baseMin), Math.Min(10.0, baseMax + 0.5));
            kq.DiemGK = chiTiet.TryGetValue("col_gk", out var gkVal) ? gkVal : SampleScore(baseMin, baseMax);
            kq.ChiTietDiemQT = JsonSerializer.Serialize(chiTiet);

            double qtSum = cauHinh.Sum(c => (chiTiet[c.id] ?? 0) * (c.weight / 100.0));
            kq.DiemCK = Math.Round(qtSum, 1, MidpointRounding.AwayFromZero);

            if (tyLeThi == 0)
            {
                kq.DiemThiLan1 = null;
                kq.DiemThiLan2 = null;
            }
            else
            {
                kq.DiemThiLan1 = SampleScore(baseMin, baseMax);
                double tk1 = Math.Round((kq.DiemCK.Value * (tyLeQT / 100.0)) + (kq.DiemThiLan1.Value * (tyLeThi / 100.0)), 1, MidpointRounding.AwayFromZero);

                // Chỉ sinh Điểm Thi Lần 2 nếu Tổng kết Lần 1 < 4.0 (rớt lần 1)
                if (tk1 < 4.0)
                {
                    // 65% sinh viên thi lại lần 2 qua môn (5.0 - 7.5, bị trần 6.0), 35% vẫn rớt (2.5 - 3.5)
                    bool passRetake = (hash % 3) != 0;
                    kq.DiemThiLan2 = passRetake ? SampleScore(5.2, 7.5) : SampleScore(2.5, 3.5);
                }
                else
                {
                    kq.DiemThiLan2 = null;
                }
            }

            var (tk, dc, gc) = GradeHelper.TinhDiemTongKetCuoiCung(kq.DiemCK, kq.DiemThiLan1, kq.DiemThiLan2, tyLeQT, tyLeThi);
            kq.DiemTongKet = tk;
            kq.DiemChu = dc;
            kq.GhiChu = gc;
            updated++;
        }

        if (updated > 0 && (string.IsNullOrWhiteSpace(lhp.TrangThaiNhapDiem) ||
            lhp.TrangThaiNhapDiem == "Vui lòng nhập điểm" ||
            lhp.TrangThaiNhapDiem == "Chưa nhập"))
        {
            lhp.TrangThaiNhapDiem = "Đang nhập";
        }

        return updated;
    }
}
