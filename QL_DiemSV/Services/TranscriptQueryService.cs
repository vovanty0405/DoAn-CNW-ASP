using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QL_DiemSV.Data;
using QL_DiemSV.Models;
using QL_DiemSV.ViewModels;

namespace QL_DiemSV.Services;

public sealed class TranscriptQueryService
{
    private readonly ApplicationDbContext _db;

    public TranscriptQueryService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
    }

    public sealed record QueryResult(
        bool Success,
        int StatusCode,
        string? ErrorMessage,
        List<TranscriptDocumentDto> Documents);

    private sealed record MajorCurriculumCacheEntry(
        Dictionary<string, bool> CourseTypes,
        int ReqTongTC,
        int ReqTCBatBuoc,
        int ReqTCTuChon,
        double ReqMinGPA);

    private sealed record StudentMetaRow(
        string MaSV,
        string HoTen,
        DateTime? NgaySinh,
        string? GioiTinh,
        string? TrangThai,
        string? Email,
        string? SDT,
        string? MaLop,
        string? TenLop,
        string? NienKhoa,
        string? MaNganh,
        string? TenNganh,
        string? HeDaoTao,
        string? TenKhoa);

    private sealed record GradeFlatRow(
        int MaKQ,
        string MaSV,
        string? MaLHP,
        string? TenLopHP,
        string MaMon,
        string TenMon,
        int SoTinChi,
        string MaHK,
        string TenHK,
        int NamHocBatDau,
        int NamHocKetThuc,
        DateTime? NgayBatDauTuan1,
        double? DiemCC,
        double? DiemGK,
        double? DiemCK,
        double? DiemThiLan1,
        double? DiemThiLan2,
        double? DiemTongKet,
        string? DiemChu,
        string? GhiChu);

    /// <summary>
    /// Lấy bảng điểm của 1 sinh viên, kiểm tra phạm vi và đọc kết quả đã công bố trực tiếp từ SQL.
    /// </summary>
    public async Task<QueryResult> GetSingleAsync(
        string maSV,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var normalized = (maSV ?? "").Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return new QueryResult(false, 400, "Mã sinh viên không được để trống!", new());
        }

        return await GetBatchAsync(new[] { normalized }, user, cancellationToken);
    }

    /// <summary>
    /// Lấy bảng điểm cho 1 hoặc nhiều sinh viên với SQL Projection gọn nhẹ, không cache dữ liệu học tập,
    /// để JSON và PDF đều phản ánh dữ liệu đã lưu ở lần yêu cầu mới.
    /// </summary>
    public async Task<QueryResult> GetBatchAsync(
        IEnumerable<string> rawStudentIds,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (rawStudentIds == null)
        {
            return new QueryResult(false, 400, "Danh sách mã sinh viên không hợp lệ!", new());
        }

        var orderedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawStudentIds)
        {
            var code = (raw ?? "").Trim();
            if (code.Length > 0 && seen.Add(code))
            {
                orderedIds.Add(code);
            }
        }

        if (orderedIds.Count == 0)
        {
            return new QueryResult(false, 400, "Vui lòng chọn ít nhất một sinh viên để xuất bảng điểm.", new());
        }

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "";
        if (role == "SinhVien")
        {
            var myMaSV = (user.FindFirstValue("MaSV") ?? user.Identity?.Name ?? "").Trim();
            if (orderedIds.Count > 1 || !string.Equals(myMaSV, orderedIds[0], StringComparison.OrdinalIgnoreCase))
            {
                return new QueryResult(false, 403, "Sinh viên chỉ được phép tra cứu hoặc xuất bảng điểm của chính mình!", new());
            }
        }

        // 1. Truy vấn thông tin sinh viên trong phạm vi phân quyền bằng SQL Projection (không kéo toàn bộ Entity Graph nặng)
        var scopedStudentsQuery = await StudentLookupScope.StudentsAsync(_db, user);
        var scopedStudents = await scopedStudentsQuery
            .Where(s => orderedIds.Contains(s.MaSV))
            .Select(s => new StudentMetaRow(
                s.MaSV,
                s.HoTen,
                s.NgaySinh,
                s.GioiTinh,
                s.TrangThai,
                s.Email,
                s.SDT,
                s.MaLop,
                s.LopHanhChinh != null ? s.LopHanhChinh.TenLop : null,
                s.LopHanhChinh != null ? s.LopHanhChinh.NienKhoa : null,
                s.LopHanhChinh != null ? s.LopHanhChinh.MaNganh : null,
                s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null ? s.LopHanhChinh.Nganh.TenNganh : null,
                s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null ? s.LopHanhChinh.Nganh.HeDaoTao : null,
                s.LopHanhChinh != null && s.LopHanhChinh.Nganh != null && s.LopHanhChinh.Nganh.Khoa != null ? s.LopHanhChinh.Nganh.Khoa.TenKhoa : null
            ))
            .ToListAsync(cancellationToken);

        var studentByCode = scopedStudents.ToDictionary(s => s.MaSV, StringComparer.OrdinalIgnoreCase);

        // Nếu có mã sinh viên không nằm trong kết quả scoped -> kiểm tra xem do không tồn tại (404) hay không có quyền (403)
        if (studentByCode.Count < orderedIds.Count)
        {
            var missingCodes = orderedIds.Where(id => !studentByCode.ContainsKey(id)).ToList();
            var existingInDb = (await _db.SinhViens.AsNoTracking()
                .Where(s => missingCodes.Contains(s.MaSV))
                .Select(s => s.MaSV)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var notFoundIds = missingCodes.Where(id => !existingInDb.Contains(id)).ToList();
            if (notFoundIds.Count > 0)
            {
                return new QueryResult(
                    false,
                    404,
                    notFoundIds.Count == 1
                        ? $"Không tìm thấy sinh viên có mã {notFoundIds[0]}!"
                        : $"Không tìm thấy {notFoundIds.Count} sinh viên trong hệ thống: {string.Join(", ", notFoundIds.Take(10))}",
                    new());
            }

            return new QueryResult(
                false,
                403,
                $"Bạn không có quyền truy cập bảng điểm của sinh viên: {string.Join(", ", missingCodes.Take(10))}",
                new());
        }

        // Read each transcript from SQL on every request; never reuse unpublished/stale grades.
        var cachedDocs = new Dictionary<string, TranscriptDocumentDto>(StringComparer.OrdinalIgnoreCase);
        var uncachedStudentIds = orderedIds;

        if (uncachedStudentIds.Count > 0)
        {
            // 3. Lấy Khung chương trình & Quy định tốt nghiệp của các ngành (đọc mới theo MaNganh)
            var neededMajors = uncachedStudentIds
                .Select(code => studentByCode[code].MaNganh)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var majorConfigMap = await GetOrLoadMajorConfigsAsync(neededMajors, cancellationToken);

            // 4. Truy vấn phẳng (Flat Projection) chỉ các cột cần thiết của các sổ điểm đã khóa
            // Không kéo cột chuỗi JSON ChiTietDiemQT hay CauHinhDiem
            var flatGrades = await _db.KetQuaHocTaps.AsNoTracking()
                .Where(k => k.MaSV != null && uncachedStudentIds.Contains(k.MaSV)
                    && k.LopHocPhan != null && k.LopHocPhan.TrangThaiNhapDiem == "Đã khóa"
                    && k.LopHocPhan.HocKy != null && k.LopHocPhan.MonHoc != null)
                .Select(k => new GradeFlatRow(
                    k.MaKQ,
                    k.MaSV!,
                    k.MaLHP,
                    k.LopHocPhan!.TenLopHP,
                    k.LopHocPhan.MonHoc!.MaMon,
                    k.LopHocPhan.MonHoc.TenMon,
                    k.LopHocPhan.MonHoc.SoTinChi,
                    k.LopHocPhan.HocKy!.MaHK,
                    k.LopHocPhan.HocKy.TenHK,
                    k.LopHocPhan.HocKy.NamHocBatDau,
                    k.LopHocPhan.HocKy.NamHocKetThuc,
                    k.LopHocPhan.HocKy.NgayBatDauTuan1,
                    k.DiemCC,
                    k.DiemGK,
                    k.DiemCK,
                    k.DiemThiLan1,
                    k.DiemThiLan2,
                    k.DiemTongKet,
                    k.DiemChu,
                    k.GhiChu
                ))
                .ToListAsync(cancellationToken);

            var gradesByStudent = flatGrades
                .GroupBy(g => g.MaSV, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var code in uncachedStudentIds)
            {
                var sv = studentByCode[code];
                var majorEntry = !string.IsNullOrWhiteSpace(sv.MaNganh) && majorConfigMap.TryGetValue(sv.MaNganh, out var mc)
                    ? mc
                    : new MajorCurriculumCacheEntry(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase), 120, 100, 20, 2.0);

                var studentFlatRows = gradesByStudent.TryGetValue(sv.MaSV, out var rows)
                    ? rows
                    : new List<GradeFlatRow>();

                var doc = BuildTranscriptDocument(sv, majorEntry, studentFlatRows);
                cachedDocs[code] = doc;
            }
        }

        var finalDocuments = orderedIds.Select(code => cachedDocs[code]).ToList();
        return new QueryResult(true, 200, null, finalDocuments);
    }

    private async Task<Dictionary<string, MajorCurriculumCacheEntry>> GetOrLoadMajorConfigsAsync(
        List<string> majorCodes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, MajorCurriculumCacheEntry>(StringComparer.OrdinalIgnoreCase);
        var missingMajors = majorCodes;

        if (missingMajors.Count == 0)
            return result;

        var khungRows = await _db.KhungChuongTrinhs.AsNoTracking()
            .Include(k => k.MonHoc)
            .Where(k => k.MaNganh != null && missingMajors.Contains(k.MaNganh))
            .ToListAsync(cancellationToken);

        var quyDinhRows = await _db.QuyDinhTotNghieps.AsNoTracking()
            .Where(q => missingMajors.Contains(q.MaNganh))
            .ToListAsync(cancellationToken);

        var khungByMajor = khungRows
            .Where(k => k.MaNganh != null)
            .GroupBy(k => k.MaNganh!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var rulesByMajor = quyDinhRows.ToDictionary(q => q.MaNganh, StringComparer.OrdinalIgnoreCase);

        foreach (var major in missingMajors)
        {
            var kRows = khungByMajor.TryGetValue(major, out var list) ? list : new List<KhungChuongTrinh>();
            rulesByMajor.TryGetValue(major, out var qd);
            var courseTypes = GraduationRules.CourseTypes(kRows);
            var (reqTong, reqBB, reqTC, reqGpa) = GraduationRules.Requirements(kRows, qd);

            var entry = new MajorCurriculumCacheEntry(courseTypes, reqTong, reqBB, reqTC, reqGpa);
            result[major] = entry;
        }

        return result;
    }

    private static TranscriptDocumentDto BuildTranscriptDocument(
        StudentMetaRow sv,
        MajorCurriculumCacheEntry majorCfg,
        List<GradeFlatRow> flatRows)
    {
        var khungCT = majorCfg.CourseTypes;
        int reqTongTC = majorCfg.ReqTongTC;
        int reqTCBatBuoc = majorCfg.ReqTCBatBuoc;
        int reqTCTuChon = majorCfg.ReqTCTuChon;
        double reqMinGPA = majorCfg.ReqMinGPA;

        // Chuyển sang đối tượng nhẹ tương thích hoàn toàn với TranscriptStatistics & GraduationRules
        var ketQuas = flatRows.Select(r => new KetQuaHocTap
        {
            MaKQ = r.MaKQ,
            MaSV = r.MaSV,
            MaLHP = r.MaLHP,
            DiemCC = r.DiemCC,
            DiemGK = r.DiemGK,
            DiemCK = r.DiemCK,
            DiemThiLan1 = r.DiemThiLan1,
            DiemThiLan2 = r.DiemThiLan2,
            DiemTongKet = r.DiemTongKet,
            DiemChu = r.DiemChu,
            GhiChu = r.GhiChu,
            LopHocPhan = new LopHocPhan
            {
                MaLHP = r.MaLHP ?? "",
                TenLopHP = r.TenLopHP,
                MaMon = r.MaMon,
                MaHK = r.MaHK,
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
                    NamHocKetThuc = r.NamHocKetThuc,
                    NgayBatDauTuan1 = r.NgayBatDauTuan1
                }
            }
        }).ToList();

        var completedSoFar = new List<KetQuaHocTap>();
        var groupedByHK = ketQuas
            .GroupBy(k => k.LopHocPhan!.MaHK!)
            .OrderBy(g => g.First().LopHocPhan!.HocKy!.NamHocBatDau)
            .ThenBy(g => TranscriptStatistics.TermOrder(g.First().LopHocPhan!.HocKy!))
            .ThenBy(g => g.First().LopHocPhan!.HocKy!.NgayBatDauTuan1)
            .ThenBy(g => g.Key)
            .Select(g =>
            {
                var hk = g.First().LopHocPhan!.HocKy!;
                var danhSachMon = g
                    .OrderBy(k => k.LopHocPhan!.MonHoc!.MaMon)
                    .ThenBy(k => k.MaKQ)
                    .Select(k =>
                    {
                        var mh = k.LopHocPhan!.MonHoc!;
                        string loaiMon = khungCT.GetValueOrDefault(mh.MaMon) ? "Bắt buộc" : "Tự chọn";
                        double? diemHe4 = k.DiemTongKet != null ? GradeHelper.QuyDoiHe4(k.DiemTongKet.Value) : null;
                        bool quaMon = k.DiemTongKet != null && k.DiemTongKet.Value >= 4.0;

                        return new TranscriptCourseRowDto
                        {
                            MaKQ = k.MaKQ,
                            MaLHP = k.MaLHP,
                            TenLopHP = k.LopHocPhan!.TenLopHP,
                            MaMon = mh.MaMon,
                            TenMon = mh.TenMon,
                            SoTinChi = mh.SoTinChi,
                            LoaiMon = loaiMon,
                            DiemQT = k.DiemCK,
                            DiemCC = k.DiemCC,
                            DiemGK = k.DiemGK,
                            DiemCK = k.DiemCK,
                            DiemThiLan1 = k.DiemThiLan1,
                            DiemThiLan2 = k.DiemThiLan2,
                            DiemTongKet = k.DiemTongKet,
                            DiemHe4 = diemHe4,
                            DiemChu = k.DiemChu,
                            GhiChu = k.GhiChu,
                            QuaMon = quaMon
                        };
                    }).ToList();

                var semesterTotals = TranscriptStatistics.Calculate(g);
                completedSoFar.AddRange(g);
                var accumulatedTotals = TranscriptStatistics.Calculate(completedSoFar, cumulative: true);

                return new TranscriptSemesterDto
                {
                    MaHK = hk.MaHK,
                    TenHK = hk.TenHK,
                    NamHoc = $"{hk.NamHocBatDau} - {hk.NamHocKetThuc}",
                    NamHocBatDau = hk.NamHocBatDau,
                    TongTinChiHK = semesterTotals.TinChiDat,
                    GPA_HK_He10 = semesterTotals.GPAHe10,
                    GPA_HK_He4 = semesterTotals.GPAHe4,
                    ThongKeHocKy = semesterTotals,
                    TichLuyDenHocKy = accumulatedTotals,
                    DanhSachMon = danhSachMon
                };
            })
            .ToList();

        var overallTotals = TranscriptStatistics.Calculate(ketQuas, cumulative: true);
        var graduation = GraduationRules.Evaluate(
            ketQuas,
            khungCT,
            overallTotals.GPAHe4,
            reqTongTC,
            reqTCBatBuoc,
            reqTCTuChon,
            reqMinGPA);

        int tcTichLuy = graduation.PassedCredits;
        int tcBatBuocDat = graduation.RequiredCredits;
        int tcTuChonDat = graduation.ElectiveCredits;
        int soMonNo = graduation.FailedCourses;
        int tcNo = graduation.FailedCredits;

        var lyDoKhongDat = new List<string>();
        if (tcTichLuy < reqTongTC) lyDoKhongDat.Add($"Thiếu {reqTongTC - tcTichLuy} tín chỉ tích lũy ({tcTichLuy}/{reqTongTC} TC)");
        if (tcBatBuocDat < reqTCBatBuoc) lyDoKhongDat.Add($"Thiếu {reqTCBatBuoc - tcBatBuocDat} tín chỉ bắt buộc ({tcBatBuocDat}/{reqTCBatBuoc} TC)");
        if (tcTuChonDat < reqTCTuChon) lyDoKhongDat.Add($"Thiếu {reqTCTuChon - tcTuChonDat} tín chỉ tự chọn ({tcTuChonDat}/{reqTCTuChon} TC)");
        if ((overallTotals.GPAHe4 ?? 0) < reqMinGPA) lyDoKhongDat.Add($"GPA tích lũy hệ 4 ({(overallTotals.GPAHe4 ?? 0):0.00}) thấp hơn mức tối thiểu ({reqMinGPA:0.00})");
        if (soMonNo > 0) lyDoKhongDat.Add($"Còn nợ {soMonNo} môn học ({tcNo} tín chỉ)");
        if (graduation.MissingRequiredCourses > 0) lyDoKhongDat.Add($"Còn {graduation.MissingRequiredCourses} môn bắt buộc chưa đạt");

        return new TranscriptDocumentDto
        {
            ThongTinSinhVien = new TranscriptStudentInfoDto
            {
                MaSV = sv.MaSV,
                HoTen = sv.HoTen,
                NgaySinh = sv.NgaySinh,
                GioiTinh = sv.GioiTinh,
                TrangThai = sv.TrangThai,
                Email = sv.Email,
                SDT = sv.SDT,
                MaLop = sv.MaLop,
                TenLop = sv.TenLop,
                NienKhoa = sv.NienKhoa,
                TenNganh = sv.TenNganh,
                TenKhoa = sv.TenKhoa,
                HeDaoTao = sv.HeDaoTao
            },
            TongKetToanKhoa = new TranscriptOverallSummaryDto
            {
                TongTinChiTichLuy = tcTichLuy,
                TCBatBuocDat = tcBatBuocDat,
                TCTuChonDat = tcTuChonDat,
                GPA_He10 = overallTotals.GPAHe10,
                GPA_He4 = overallTotals.GPAHe4,
                ThongKe = overallTotals,
                XepLoai = overallTotals.XepLoai,
                SoMonNo = soMonNo,
                SoTinChiNo = tcNo,
                XetTotNghiep = new TranscriptGraduationEligibilityDto
                {
                    DuDieuKien = graduation.Eligible,
                    LyDo = lyDoKhongDat,
                    QuyDinh = new TranscriptGraduationRuleDto
                    {
                        TongTTC = reqTongTC,
                        TCBatBuoc = reqTCBatBuoc,
                        TCTuChon = reqTCTuChon,
                        MinGPA = reqMinGPA
                    }
                }
            },
            KetQuaTheoHocKy = groupedByHK
        };
    }
}
