using System.Globalization;
using Microsoft.Extensions.Options;
using QL_DiemSV.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QL_DiemSV.Services;

public sealed class TranscriptPdfService
{
    private readonly TranscriptPdfOptions _options;
    private static readonly string PreferredFontFamily = ResolveVietnameseFontFamily();

    static TranscriptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public TranscriptPdfService(IOptions<TranscriptPdfOptions> options)
    {
        _options = options.Value ?? new TranscriptPdfOptions();
    }

    public int MaxStudentsPerBatch => Math.Clamp(_options.MaxStudentsPerBatch, 1, 200);

    private static bool _isWarmedUp;

    /// <summary>
    /// Khởi động trước (Pre-warm) engine Skia/HarfBuzz & nạp glyph tiếng Việt của QuestPDF
    /// ngay khi ứng dụng khởi chạy để lần bấm In PDF đầu tiên của người dùng phản hồi dưới 50ms.
    /// </summary>
    public void WarmUp()
    {
        if (_isWarmedUp) return;
        try
        {
            var dummy = new TranscriptDocumentDto
            {
                ThongTinSinhVien = new TranscriptStudentInfoDto
                {
                    MaSV = "WARMUP",
                    HoTen = "Nguyễn Văn A",
                    MaLop = "DH22",
                    TenNganh = "Kỹ thuật phần mềm",
                    TenKhoa = "Công nghệ Thông tin"
                }
            };
            _ = GenerateSinglePdf(dummy);
            _isWarmedUp = true;
        }
        catch
        {
            // Bỏ qua lỗi warmup nếu có
        }
    }

    private static string ResolveVietnameseFontFamily()
    {
        // Ưu tiên Times New Roman trên Windows, fallback sang Arial hoặc Segoe UI
        return OperatingSystem.IsWindows() ? "Times New Roman" : "DejaVu Serif";
    }

    /// <summary>
    /// Xuất PDF bảng điểm cho 1 sinh viên.
    /// </summary>
    public byte[] GenerateSinglePdf(TranscriptDocumentDto studentDoc)
    {
        if (studentDoc == null)
            throw new ArgumentNullException(nameof(studentDoc));

        return GenerateBatchPdf(new[] { studentDoc });
    }

    /// <summary>
    /// Xuất 1 file PDF duy nhất chứa nhiều bảng điểm sinh viên độc lập.
    /// Mỗi sinh viên được tạo trong một khối Page riêng biệt (luôn bắt đầu trên trang A4 mới),
    /// tự động phân trang khi có nhiều học kỳ/môn học.
    /// </summary>
    public byte[] GenerateBatchPdf(IReadOnlyList<TranscriptDocumentDto> studentDocs)
    {
        if (studentDocs == null || studentDocs.Count == 0)
            throw new ArgumentException("Danh sách bảng điểm cần xuất trống.", nameof(studentDocs));

        var issueDate = DateTime.Now;

        var document = Document.Create(container =>
        {
            for (int idx = 0; idx < studentDocs.Count; idx++)
            {
                var doc = studentDocs[idx];
                int studentOrder = idx + 1;
                int totalStudents = studentDocs.Count;

                // Mỗi gọi container.Page(...) tạo một Section độc lập bắt đầu trên trang A4 mới
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(1.5f, Unit.Centimetre);
                    page.MarginBottom(1.4f, Unit.Centimetre);
                    page.MarginLeft(1.6f, Unit.Centimetre);
                    page.MarginRight(1.6f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x
                        .FontFamily(PreferredFontFamily)
                        .FontSize(10)
                        .FontColor(Colors.Black));

                    page.Content().Element(c => ComposeStudentTranscript(c, doc, issueDate));

                    page.Footer().Element(f => ComposePageFooter(f, doc.ThongTinSinhVien, studentOrder, totalStudents, issueDate));
                });
            }
        });

        return document.GeneratePdf();
    }

    private void ComposeStudentTranscript(IContainer container, TranscriptDocumentDto doc, DateTime issueDate)
    {
        var sv = doc.ThongTinSinhVien;
        var tk = doc.TongKetToanKhoa;

        container.Column(col =>
        {
            col.Spacing(8);

            // 1. Quốc hiệu, Tiêu ngữ & Tên trường
            col.Item().Element(ComposeAdministrativeHeader);

            // 2. Tiêu đề chính: BẢNG ĐIỂM SINH VIÊN
            col.Item().PaddingTop(4).PaddingBottom(4).Column(titleCol =>
            {
                titleCol.Item().AlignCenter().Text("BẢNG ĐIỂM SINH VIÊN")
                    .FontSize(15).Bold();
                titleCol.Item().PaddingTop(2).AlignCenter().Text("KẾT QUẢ HỌC TẬP TOÀN KHÓA")
                    .FontSize(10.5f).Bold();
            });

            // 3. Thông tin hành chính của Sinh viên
            col.Item().Element(c => ComposeStudentInfoSection(c, sv));

            // 4. Bảng điểm theo từng Học kỳ (Sắp xếp từ cũ đến mới)
            if (doc.KetQuaTheoHocKy.Count == 0)
            {
                col.Item().PaddingVertical(14).Border(0.75f).BorderColor(Colors.Grey.Medium)
                    .Padding(12).AlignCenter()
                    .Text("Sinh viên hiện chưa có kết quả học phần nào được ghi nhận trong hệ thống.")
                    .Italic().FontSize(10);
            }
            else
            {
                foreach (var hk in doc.KetQuaTheoHocKy)
                {
                    col.Item().Element(c => ComposeSemesterSection(c, hk));
                }
            }

            // 5. Tổng kết toàn khóa & Xét điều kiện tốt nghiệp + Khung xác nhận nhà trường
            // Gom chung hoặc giữ khối xác nhận không bị cắt rời trang
            col.Item().PaddingTop(4).Element(c => ComposeOverallSummaryAndSignature(c, tk, issueDate));
        });
    }

    private void ComposeAdministrativeHeader(IContainer container)
    {
        var supervisingOrg = string.IsNullOrWhiteSpace(_options.SupervisingOrganization)
            ? "BỘ GIÁO DỤC VÀ ĐÀO TẠO"
            : _options.SupervisingOrganization.Trim().ToUpperInvariant();

        var schoolName = string.IsNullOrWhiteSpace(_options.SchoolName)
            ? "TRƯỜNG ĐẠI HỌC CÔNG NGHỆ & ĐÀO TẠO ACADEMIC LEDGER"
            : _options.SchoolName.Trim().ToUpperInvariant();

        container.Row(row =>
        {
            // Bên trái: Cơ quan chủ quản & Tên trường
            row.RelativeItem(5).Column(left =>
            {
                left.Item().AlignCenter().Text(supervisingOrg)
                    .FontSize(9.5f);
                left.Item().PaddingTop(1).AlignCenter().Text(schoolName)
                    .FontSize(10f).Bold();
                left.Item().PaddingTop(3).AlignCenter().Width(110).LineHorizontal(0.75f).LineColor(Colors.Black);
            });

            // Bên phải: Quốc hiệu & Tiêu ngữ
            row.RelativeItem(5).Column(right =>
            {
                right.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
                    .FontSize(9.5f).Bold();
                right.Item().PaddingTop(1).AlignCenter().Text("Độc lập - Tự do - Hạnh phúc")
                    .FontSize(10f).Bold();
                right.Item().PaddingTop(3).AlignCenter().Width(130).LineHorizontal(0.75f).LineColor(Colors.Black);
            });
        });
    }

    private static void ComposeStudentInfoSection(IContainer container, TranscriptStudentInfoDto sv)
    {
        string ngaySinhStr = sv.NgaySinh.HasValue
            ? sv.NgaySinh.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "Chưa cập nhật";

        string gioiTinhStr = string.IsNullOrWhiteSpace(sv.GioiTinh) ? "—" : sv.GioiTinh;
        string lopStr = !string.IsNullOrWhiteSpace(sv.MaLop)
            ? (!string.IsNullOrWhiteSpace(sv.TenLop) ? $"{sv.MaLop} ({sv.TenLop})" : sv.MaLop)
            : "—";
        string nienKhoaStr = string.IsNullOrWhiteSpace(sv.NienKhoa) ? "—" : sv.NienKhoa;
        string nganhStr = string.IsNullOrWhiteSpace(sv.TenNganh) ? "—" : sv.TenNganh;
        string khoaStr = string.IsNullOrWhiteSpace(sv.TenKhoa) ? "—" : sv.TenKhoa;
        string heDaoTaoStr = string.IsNullOrWhiteSpace(sv.HeDaoTao) ? "Chính quy" : sv.HeDaoTao;
        string trangThaiStr = string.IsNullOrWhiteSpace(sv.TrangThai) ? "Đang học" : sv.TrangThai;

        container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Darken2)
            .Padding(8)
            .Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(5.5f);
                    cols.RelativeColumn(4.5f);
                });

                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Họ và tên sinh viên: ").FontSize(9.5f);
                    txt.Span(sv.HoTen).FontSize(10f).Bold();
                });
                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Mã số sinh viên: ").FontSize(9.5f);
                    txt.Span(sv.MaSV).FontSize(10f).Bold();
                });

                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Ngày sinh: ").FontSize(9.5f);
                    txt.Span(ngaySinhStr).FontSize(9.5f).Bold();
                    txt.Span("   |   Giới tính: ").FontSize(9.5f);
                    txt.Span(gioiTinhStr).FontSize(9.5f).Bold();
                });
                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Lớp hành chính: ").FontSize(9.5f);
                    txt.Span(lopStr).FontSize(9.5f).Bold();
                });

                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Ngành đào tạo: ").FontSize(9.5f);
                    txt.Span(nganhStr).FontSize(9.5f).Bold();
                });
                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Khóa học / Niên khóa: ").FontSize(9.5f);
                    txt.Span(nienKhoaStr).FontSize(9.5f).Bold();
                });

                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Khoa quản lý: ").FontSize(9.5f);
                    txt.Span(khoaStr).FontSize(9.5f).Bold();
                });
                table.Cell().PaddingVertical(1.5f).Text(txt =>
                {
                    txt.Span("Hệ đào tạo: ").FontSize(9.5f);
                    txt.Span(heDaoTaoStr).FontSize(9.5f).Bold();
                    txt.Span($" ({trangThaiStr})").FontSize(9f);
                });
            });
    }

    private static void ComposeSemesterSection(IContainer container, TranscriptSemesterDto hk)
    {
        var semStats = hk.ThongKeHocKy;
        var cumStats = hk.TichLuyDenHocKy;

        container.Column(col =>
        {
            // Tiêu đề Học kỳ
            col.Item()
                .Background(Colors.Grey.Lighten3)
                .Border(0.75f)
                .BorderColor(Colors.Grey.Darken2)
                .PaddingHorizontal(8)
                .PaddingVertical(4)
                .Row(row =>
                {
                    row.RelativeItem().Text($"{hk.TenHK.ToUpperInvariant()} — NĂM HỌC {hk.NamHoc} (Mã HK: {hk.MaHK})")
                        .FontSize(9.5f).Bold();
                    row.AutoItem().Text($"Số môn: {hk.DanhSachMon.Count} học phần")
                        .FontSize(9f);
                });

            // Bảng học phần của Học kỳ
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);  // 1. STT
                    columns.ConstantColumn(56);  // 2. Mã HP
                    columns.RelativeColumn(3.2f);// 3. Tên học phần
                    columns.ConstantColumn(26);  // 4. Số TC
                    columns.ConstantColumn(36);  // 5. Điểm QT
                    columns.ConstantColumn(44);  // 6. Điểm thi (L1/L2)
                    columns.ConstantColumn(38);  // 7. Tổng kết (10)
                    columns.ConstantColumn(32);  // 8. Hệ 4
                    columns.ConstantColumn(32);  // 9. Điểm chữ
                    columns.ConstantColumn(58);  // 10. Kết quả
                });

                // Lặp lại tiêu đề bảng nếu bảng học kỳ dài qua trang mới
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("STT");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Mã HP");
                    header.Cell().Element(HeaderCellStyle).AlignLeft().Text("Tên học phần");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("TC");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Đ.QT");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Đ.Thi");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("TK(10)");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Hệ 4");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Chữ");
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Kết quả");
                });

                for (int i = 0; i < hk.DanhSachMon.Count; i++)
                {
                    var m = hk.DanhSachMon[i];
                    int stt = i + 1;

                    string diemQtText = FormatScore(m.DiemQT);
                    string diemThiText = FormatExamScore(m.DiemThiLan1, m.DiemThiLan2);
                    string diemTk10Text = FormatScore(m.DiemTongKet);
                    string diemHe4Text = FormatScore(m.DiemHe4, "0.0");
                    string diemChuText = string.IsNullOrWhiteSpace(m.DiemChu) ? "—" : m.DiemChu;
                    string ketQuaText = m.DiemTongKet == null
                        ? "Chưa có điểm"
                        : (!string.IsNullOrWhiteSpace(m.GhiChu) ? m.GhiChu : (m.QuaMon ? "Đạt" : "Học lại"));

                    // Mỗi dòng môn học dùng ShowEntire để không bao giờ bị cắt đôi giữa 2 trang
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(stt.ToString());
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(m.MaMon).Bold();
                    table.Cell().Element(DataCellStyle).AlignLeft().Text(m.TenMon);
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(m.SoTinChi.ToString());
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(diemQtText);
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(diemThiText);
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(diemTk10Text).Bold();
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(diemHe4Text);
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(diemChuText).Bold();
                    table.Cell().Element(DataCellStyle).AlignCenter().Text(ketQuaText).FontSize(8.5f);
                }
            });

            // Thống kê cuối mỗi Học kỳ
            col.Item()
                .BorderLeft(0.75f)
                .BorderRight(0.75f)
                .BorderBottom(0.75f)
                .BorderColor(Colors.Grey.Darken2)
                .Background(Colors.Grey.Lighten5)
                .PaddingHorizontal(8)
                .PaddingVertical(4)
                .Column(statCol =>
                {
                    statCol.Item().Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.Span("• Thống kê học kỳ: ").FontSize(8.8f).Bold();
                            t.Span($"TC đăng ký: {semStats.TinChiDangKy}  |  TC đạt: {semStats.TinChiDat}  |  TC chưa đạt: {semStats.TinChiKhongDat}").FontSize(8.8f);
                            if (semStats.TinChiChuaCoDiem > 0)
                            {
                                t.Span($"  |  TC chưa có điểm: {semStats.TinChiChuaCoDiem}").FontSize(8.8f);
                            }
                        });
                        r.AutoItem().Text(t =>
                        {
                            t.Span("GPA học kỳ (Hệ 10 / Hệ 4): ").FontSize(8.8f);
                            t.Span($"{FormatGpa(semStats.GPAHe10)} / {FormatGpa(semStats.GPAHe4)}").FontSize(9f).Bold();
                            t.Span($" ({semStats.XepLoai})").FontSize(8.8f);
                        });
                    });

                    statCol.Item().PaddingTop(1.5f).Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.Span("• Tích lũy đến học kỳ này: ").FontSize(8.8f).Bold();
                            t.Span($"TC tích lũy đạt: {cumStats.TinChiDat} TC").FontSize(8.8f);
                        });
                        r.AutoItem().Text(t =>
                        {
                            t.Span("GPA tích lũy (Hệ 10 / Hệ 4): ").FontSize(8.8f);
                            t.Span($"{FormatGpa(cumStats.GPAHe10)} / {FormatGpa(cumStats.GPAHe4)}").FontSize(9f).Bold();
                        });
                    });
                });
        });
    }

    private void ComposeOverallSummaryAndSignature(
        IContainer container,
        TranscriptOverallSummaryDto tk,
        DateTime issueDate)
    {
        var totals = tk.ThongKe;
        var xet = tk.XetTotNghiep;
        var qd = xet.QuyDinh;

        string location = string.IsNullOrWhiteSpace(_options.DocumentLocation)
            ? "TP. Hồ Chí Minh"
            : _options.DocumentLocation.Trim();

        string signerTitle = string.IsNullOrWhiteSpace(_options.SignerTitle)
            ? "TL. HIỆU TRƯỞNG / TRƯỞNG PHÒNG ĐÀO TẠO"
            : _options.SignerTitle.Trim().ToUpperInvariant();

        // ShowEntire đảm bảo khối Tổng kết + Chữ ký không bị đứt gãy rời rạc sang trang trắng
        container.ShowEntire().Column(col =>
        {
            col.Spacing(10);

            // Khung Tổng kết toàn khóa
            col.Item()
                .Border(0.9f)
                .BorderColor(Colors.Black)
                .Padding(8)
                .Column(sumCol =>
                {
                    sumCol.Item().PaddingBottom(4).Text("TỔNG KẾT KẾT QUẢ HỌC TẬP TOÀN KHÓA & XÉT ĐIỀU KIỆN TỐT NGHIỆP")
                        .FontSize(10f).Bold();

                    sumCol.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(5);
                        });

                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Tổng số tín chỉ đăng ký (không trùng môn): ").FontSize(9.2f);
                            txt.Span($"{totals.TinChiDangKy} TC").FontSize(9.5f).Bold();
                        });
                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Điểm trung bình tích lũy hệ 10: ").FontSize(9.2f);
                            txt.Span(FormatGpa(tk.GPA_He10)).FontSize(9.5f).Bold();
                        });

                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Tổng số tín chỉ tích lũy đạt: ").FontSize(9.2f);
                            txt.Span($"{tk.TongTinChiTichLuy} / {qd.TongTTC} TC").FontSize(9.5f).Bold();
                        });
                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Điểm trung bình tích lũy hệ 4: ").FontSize(9.2f);
                            txt.Span($"{FormatGpa(tk.GPA_He4)} / 4.00").FontSize(9.5f).Bold();
                        });

                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Tín chỉ Bắt buộc / Tự chọn đạt: ").FontSize(9.2f);
                            txt.Span($"BB: {tk.TCBatBuocDat}/{qd.TCBatBuoc} TC  |  TC: {tk.TCTuChonDat}/{qd.TCTuChon} TC").FontSize(9.2f).Bold();
                        });
                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Xếp loại học lực toàn khóa: ").FontSize(9.2f);
                            txt.Span(tk.XepLoai).FontSize(9.5f).Bold();
                        });

                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Tín chỉ chưa đạt / Chưa có điểm: ").FontSize(9.2f);
                            txt.Span($"Chưa đạt: {totals.TinChiKhongDat} TC  |  Chưa có điểm: {totals.TinChiChuaCoDiem} TC").FontSize(9.2f);
                        });
                        t.Cell().PaddingVertical(1.5f).Text(txt =>
                        {
                            txt.Span("Kết quả xét điều kiện tốt nghiệp: ").FontSize(9.2f);
                            txt.Span(xet.DuDieuKien ? "ĐỦ ĐIỀU KIỆN TỐT NGHIỆP" : "CHƯA ĐỦ ĐIỀU KIỆN TỐT NGHIỆP")
                                .FontSize(9.2f).Bold();
                        });
                    });

                    if (!xet.DuDieuKien && xet.LyDo.Count > 0)
                    {
                        sumCol.Item().PaddingTop(3).Text(txt =>
                        {
                            txt.Span("Chi tiết điều kiện còn thiếu: ").FontSize(8.8f).Bold();
                            txt.Span(string.Join("; ", xet.LyDo)).FontSize(8.8f).Italic();
                        });
                    }
                });

            // Khu vực Xác nhận nhà trường ở cuối bảng điểm của mỗi sinh viên
            col.Item().PaddingTop(4).Row(row =>
            {
                // Bên trái: Ghi chú tình trạng bản in tra cứu
                row.RelativeItem(5).PaddingTop(6).Column(left =>
                {
                    left.Item().Text("Ghi chú học vụ:").FontSize(8.8f).Bold();
                    left.Item().PaddingTop(2).Text("1. Điểm tích lũy chỉ tính các học phần đã có điểm tổng kết; lấy điểm cao nhất của mỗi môn học.")
                        .FontSize(8.2f).Italic();
                    left.Item().PaddingTop(1).Text("2. Tài liệu này là bản kết xuất từ hệ thống quản lý đào tạo; chỉ có giá trị pháp lý chính thức khi có chữ ký và đóng dấu đỏ của Nhà trường.")
                        .FontSize(8.2f).Italic();
                });

                // Bên phải: Khối ngày tháng & Chữ ký xác nhận
                row.RelativeItem(5).Column(right =>
                {
                    right.Item().AlignCenter()
                        .Text($"{location}, ngày {issueDate:dd} tháng {issueDate:MM} năm {issueDate:yyyy}")
                        .FontSize(9.5f).Italic();
                    right.Item().PaddingTop(3).AlignCenter()
                        .Text("XÁC NHẬN CỦA NHÀ TRƯỜNG")
                        .FontSize(10f).Bold();
                    right.Item().PaddingTop(1).AlignCenter()
                        .Text(signerTitle)
                        .FontSize(9f).Bold();
                    right.Item().PaddingTop(1).AlignCenter()
                        .Text("(Ký, ghi rõ họ tên và đóng dấu)")
                        .FontSize(8.5f).Italic();

                    // Khoảng trắng dành cho chữ ký và con dấu thật (không tạo chữ ký/con dấu giả)
                    right.Item().Height(58);
                });
            });
        });
    }

    private void ComposePageFooter(
        IContainer container,
        TranscriptStudentInfoDto sv,
        int studentOrder,
        int totalStudents,
        DateTime issueDate)
    {
        var footerNote = string.IsNullOrWhiteSpace(_options.TranscriptFooterText)
            ? "Bản kết xuất từ Hệ thống Quản lý Điểm & Học vụ Số Academic Ledger"
            : _options.TranscriptFooterText.Trim();

        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text(txt =>
                {
                    txt.Span($"{footerNote} • SV: {sv.MaSV} - {sv.HoTen}").FontSize(8f).FontColor(Colors.Grey.Darken2);
                    if (totalStudents > 1)
                    {
                        txt.Span($" (Hồ sơ {studentOrder}/{totalStudents})").FontSize(8f).FontColor(Colors.Grey.Darken2);
                    }
                });

                row.AutoItem().Text(txt =>
                {
                    txt.Span($"In lúc {issueDate:dd/MM/yyyy HH:mm}  |  Trang ").FontSize(8f).FontColor(Colors.Grey.Darken2);
                    txt.CurrentPageNumber().FontSize(8f).Bold().FontColor(Colors.Black);
                    txt.Span(" / ").FontSize(8f).FontColor(Colors.Grey.Darken2);
                    txt.TotalPages().FontSize(8f).Bold().FontColor(Colors.Black);
                });
            });
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Border(0.6f)
            .BorderColor(Colors.Grey.Darken2)
            .Background(Colors.Grey.Lighten4)
            .PaddingVertical(3.5f)
            .PaddingHorizontal(3)
            .DefaultTextStyle(x => x.FontSize(8.8f).Bold());
    }

    private static IContainer DataCellStyle(IContainer container)
    {
        return container
            .ShowEntire()
            .Border(0.5f)
            .BorderColor(Colors.Grey.Medium)
            .PaddingVertical(3f)
            .PaddingHorizontal(3.5f)
            .DefaultTextStyle(x => x.FontSize(8.8f));
    }

    private static string FormatScore(double? score, string format = "0.0")
    {
        return score.HasValue ? score.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
    }

    private static string FormatExamScore(double? thi1, double? thi2)
    {
        if (!thi1.HasValue && !thi2.HasValue) return "—";
        if (thi1.HasValue && !thi2.HasValue) return thi1.Value.ToString("0.0", CultureInfo.InvariantCulture);
        if (!thi1.HasValue && thi2.HasValue) return $"L2: {thi2.Value.ToString("0.0", CultureInfo.InvariantCulture)}";
        return $"{thi1!.Value.ToString("0.0", CultureInfo.InvariantCulture)} / {thi2!.Value.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    private static string FormatGpa(double? gpa)
    {
        return gpa.HasValue ? gpa.Value.ToString("0.00", CultureInfo.InvariantCulture) : "—";
    }
}
