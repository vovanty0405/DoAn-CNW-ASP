namespace QL_DiemSV.ViewModels;

public sealed class TranscriptPdfOptions
{
    public const string SectionName = "TranscriptPdf";

    public string SchoolName { get; set; } = "TRƯỜNG ĐẠI HỌC CÔNG NGHỆ & ĐÀO TẠO ACADEMIC LEDGER";
    public string SupervisingOrganization { get; set; } = "BỘ GIÁO DỤC VÀ ĐÀO TẠO";
    public string? SchoolLogoPath { get; set; }
    public string SignerTitle { get; set; } = "TL. HIỆU TRƯỞNG / TRƯỞNG PHÒNG ĐÀO TẠO";
    public string DocumentLocation { get; set; } = "TP. Hồ Chí Minh";
    public string TranscriptFooterText { get; set; } = "Bản kết xuất từ Hệ thống Quản lý Điểm & Học vụ Số Academic Ledger (Chưa đóng dấu xác nhận).";
    public int MaxStudentsPerBatch { get; set; } = 50;
}
