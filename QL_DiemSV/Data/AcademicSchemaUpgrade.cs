using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Models;

namespace QL_DiemSV.Data;

// Additive, repeatable upgrade for existing databases created with EnsureCreated.
public static class AcademicSchemaUpgrade
{
    public static void Apply(ApplicationDbContext db)
    {
        using (var tx = db.Database.BeginTransaction())
        {
            db.Database.ExecuteSqlRaw("""
                EXEC sp_getapplock @Resource=N'AcademicLedger.Schema', @LockMode='Exclusive', @LockOwner='Transaction';
                IF COL_LENGTH('LopHocPhan','NMH') IS NULL ALTER TABLE LopHocPhan ADD NMH nvarchar(10) NOT NULL CONSTRAINT DF_LHP_NMH DEFAULT '01';
                IF COL_LENGTH('LopHocPhan','TTH') IS NULL ALTER TABLE LopHocPhan ADD TTH nvarchar(10) NULL;
                IF COL_LENGTH('LopHocPhan','STC') IS NULL BEGIN
                    ALTER TABLE LopHocPhan ADD STC int NOT NULL CONSTRAINT DF_LHP_STC DEFAULT 0;
                    EXEC(N'UPDATE l SET STC = m.SoTinChi FROM LopHocPhan l JOIN MonHoc m ON l.MaMon=m.MaMon');
                END;
                IF COL_LENGTH('LopHocPhan','STCHP') IS NULL BEGIN
                    ALTER TABLE LopHocPhan ADD STCHP decimal(8,2) NOT NULL CONSTRAINT DF_LHP_STCHP DEFAULT 0;
                    EXEC(N'UPDATE LopHocPhan SET STCHP=STC');
                END;
                IF COL_LENGTH('LopHocPhan','MaLop') IS NULL ALTER TABLE LopHocPhan ADD MaLop nvarchar(20) NULL;
                IF COL_LENGTH('HocKy','NgayBatDauTuan1') IS NULL ALTER TABLE HocKy ADD NgayBatDauTuan1 datetime2 NULL;
                IF COL_LENGTH('HocKy','BatDauDangKy') IS NULL ALTER TABLE HocKy ADD BatDauDangKy datetime2 NULL;
                IF COL_LENGTH('HocKy','KetThucDangKy') IS NULL ALTER TABLE HocKy ADD KetThucDangKy datetime2 NULL;
                IF COL_LENGTH('HocKy','SoTuan') IS NULL ALTER TABLE HocKy ADD SoTuan int NOT NULL CONSTRAINT DF_HK_SoTuan DEFAULT 20;
                IF COL_LENGTH('HocKy','DonGiaTinChi') IS NULL ALTER TABLE HocKy ADD DonGiaTinChi decimal(18,2) NOT NULL CONSTRAINT DF_HK_DonGia DEFAULT 635000;
                IF COL_LENGTH('KetQuaHocTap','HocPhiDangKy') IS NULL ALTER TABLE KetQuaHocTap ADD HocPhiDangKy decimal(18,2) NULL;
                IF COL_LENGTH('KetQuaHocTap','MienGiam') IS NULL ALTER TABLE KetQuaHocTap ADD MienGiam decimal(18,2) NOT NULL CONSTRAINT DF_KQ_MienGiam DEFAULT 0;
                IF COL_LENGTH('KetQuaHocTap','NgayDangKy') IS NULL ALTER TABLE KetQuaHocTap ADD NgayDangKy datetime2 NULL;
                IF OBJECT_ID('PhongHoc','U') IS NULL CREATE TABLE PhongHoc (MaPhong nvarchar(50) NOT NULL PRIMARY KEY, SucChua int NOT NULL CHECK (SucChua > 0));
                IF OBJECT_ID('ClassSchedule','U') IS NULL BEGIN
                    CREATE TABLE ClassSchedule (
                        Id int IDENTITY PRIMARY KEY, MaLHP nvarchar(20) NOT NULL,
                        LoaiBuoiHoc bit NOT NULL, Thu int NOT NULL CHECK (Thu BETWEEN 2 AND 8),
                        TietBatDau int NOT NULL CHECK (TietBatDau BETWEEN 1 AND 12),
                        SoTiet int NOT NULL CHECK (SoTiet > 0), PhongHoc nvarchar(50) NOT NULL,
                        MaGV nvarchar(20) NOT NULL, ChuoiTuanHoc nvarchar(200) NOT NULL,
                        CONSTRAINT CK_Schedule_Periods CHECK (TietBatDau + SoTiet <= 13),
                        FOREIGN KEY (MaLHP) REFERENCES LopHocPhan(MaLHP) ON DELETE CASCADE,
                        FOREIGN KEY (MaGV) REFERENCES GiangVien(MaGV),
                        FOREIGN KEY (PhongHoc) REFERENCES PhongHoc(MaPhong));
                    CREATE INDEX IX_ClassSchedule_MaLHP ON ClassSchedule(MaLHP);
                    CREATE INDEX IX_ClassSchedule_MaGV ON ClassSchedule(MaGV);
                    CREATE INDEX IX_ClassSchedule_PhongHoc ON ClassSchedule(PhongHoc);
                END;
                IF COL_LENGTH('PhongHoc','ToaNha') IS NULL ALTER TABLE PhongHoc ADD ToaNha nvarchar(30) NOT NULL CONSTRAINT DF_Phong_ToaNha DEFAULT 'Khac';
                IF COL_LENGTH('PhongHoc','TrangThai') IS NULL ALTER TABLE PhongHoc ADD TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_Phong_TrangThai DEFAULT 'HoatDong';
                IF COL_LENGTH('ClassSchedule','LoaiLich') IS NULL ALTER TABLE ClassSchedule ADD LoaiLich nvarchar(20) NOT NULL CONSTRAINT DF_Schedule_Loai DEFAULT 'LichHoc';
                IF COL_LENGTH('Nganh','HeDaoTao') IS NULL ALTER TABLE Nganh ADD HeDaoTao nvarchar(50) NOT NULL CONSTRAINT DF_Nganh_He DEFAULT N'Chính quy';
                IF OBJECT_ID('Article','U') IS NULL BEGIN
                    CREATE TABLE Article (Id int IDENTITY PRIMARY KEY, TieuDe nvarchar(250) NOT NULL, TomTat nvarchar(1000) NOT NULL,
                        NoiDung nvarchar(max) NOT NULL, DanhMuc nvarchar(40) NOT NULL, DonViDang nvarchar(200) NOT NULL,
                        NguoiDangId int NOT NULL REFERENCES UserAccounts(UserID), NgayDang datetime2 NOT NULL,
                        GhimDauTrang bit NOT NULL, FileDinhKemUrl nvarchar(500) NULL, TenFileDinhKem nvarchar(255) NULL,
                        DungLuongFile bigint NULL, ThumbnailUrl nvarchar(500) NULL, TrangThai nvarchar(20) NOT NULL, XuatBanLuc datetime2 NULL,
                        HeDaoTao nvarchar(100) NOT NULL CONSTRAINT DF_Article_HeDaoTao DEFAULT N'Đại học chính quy');
                    CREATE INDEX IX_Article_TrangThai_DanhMuc_NgayDang ON Article(TrangThai,DanhMuc,NgayDang);
                END;
                IF COL_LENGTH('Article','HeDaoTao') IS NULL ALTER TABLE Article ADD HeDaoTao nvarchar(100) NOT NULL CONSTRAINT DF_Article_HeDaoTao DEFAULT N'Đại học chính quy';
                IF OBJECT_ID('PortalDocument','U') IS NULL CREATE TABLE PortalDocument (
                    Id int IDENTITY PRIMARY KEY,TieuDe nvarchar(250) NOT NULL,Loai nvarchar(30) NOT NULL,
                    FileUrl nvarchar(500) NOT NULL,DungLuongBytes bigint NOT NULL,HienThi bit NOT NULL);
                IF COL_LENGTH('ClassSchedule','TuanBaoNghi') IS NULL ALTER TABLE ClassSchedule ADD TuanBaoNghi nvarchar(200) NOT NULL CONSTRAINT DF_Schedule_TuanBaoNghi DEFAULT '';
                IF COL_LENGTH('LopHocPhan','TyLeQuaTrinh') IS NULL ALTER TABLE LopHocPhan ADD TyLeQuaTrinh int NOT NULL CONSTRAINT DF_LHP_TyLeQT DEFAULT 40;
                IF COL_LENGTH('LopHocPhan','TyLeThiKetThuc') IS NULL ALTER TABLE LopHocPhan ADD TyLeThiKetThuc int NOT NULL CONSTRAINT DF_LHP_TyLeThi DEFAULT 60;
                IF OBJECT_ID('Curriculums','U') IS NULL BEGIN
              CREATE TABLE Curriculums(Id int IDENTITY PRIMARY KEY,HeDaoTao nvarchar(80) NOT NULL,KhoaHoc int NOT NULL,NganhHocId nvarchar(20) NOT NULL REFERENCES Nganh(MaNganh),TenChuongTrinh nvarchar(200) NOT NULL,TrangThai bit NOT NULL,NgayTao datetime2 NOT NULL,NgayCapNhat datetime2 NOT NULL);
              CREATE UNIQUE INDEX IX_Curriculum_Key ON Curriculums(HeDaoTao,KhoaHoc,NganhHocId,TenChuongTrinh);
            END;
            IF OBJECT_ID('CurriculumSections','U') IS NULL BEGIN
              CREATE TABLE CurriculumSections(Id int IDENTITY PRIMARY KEY,CurriculumId int NOT NULL REFERENCES Curriculums(Id) ON DELETE CASCADE,ThuTu int NOT NULL CHECK(ThuTu BETWEEN 1 AND 11),TieuDeMuc nvarchar(250) NOT NULL,NoiDung nvarchar(max) NOT NULL);
              CREATE UNIQUE INDEX IX_CurriculumSection_Order ON CurriculumSections(CurriculumId,ThuTu);
            END;
            """);
            tx.Commit();
        }

        SeedDefaultArticlesIfEmpty(db);
    }

    private static void SeedDefaultArticlesIfEmpty(ApplicationDbContext db)
    {
        if (db.Articles.Any()) return;

        var author = db.UserAccounts.AsNoTracking().OrderBy(u => u.UserID).FirstOrDefault();
        if (author == null) return;

        var now = DateTime.UtcNow;
        var sampleArticles = new List<Article>
        {
            new Article
            {
                TieuDe = "Thông báo nghỉ lớp Tổ chức và cấu trúc máy tính (IT010.R12) ngày 20/11/2026",
                TomTat = "Phòng Đào tạo Đại học thông báo đến sinh viên lớp Tổ chức và cấu trúc máy tính (IT010.R12) về việc nghỉ học ngày 20/11/2026.",
                DanhMuc = "NghiBu",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Khoa Kỹ thuật Máy tính",
                NguoiDangId = author.UserID,
                NgayDang = now.AddHours(-3),
                XuatBanLuc = now.AddHours(-3),
                GhimDauTrang = true,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Phòng Đào tạo Đại học thông báo đến sinh viên lớp <strong>Tổ chức và cấu trúc máy tính (IT010.R12)</strong> về việc nghỉ học như sau:</p>
                    <table border="1" cellpadding="6" style="border-collapse: collapse; width: 100%; margin: 16px 0;">
                        <tbody>
                            <tr><th style="width: 30%; text-align: left; background-color: #f3f6fa;">CBGD</th><td><strong>TS. Nguyễn Văn An</strong></td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Khoa / Bộ môn</th><td>Khoa Kỹ thuật Máy tính</td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Môn học</th><td>Tổ chức và cấu trúc máy tính</td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Lớp học phần</th><td><strong>IT010.R12 (LHP002)</strong></td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Phòng học</th><td>Phòng A102</td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Thời gian nghỉ</th><td>Tiết bắt đầu: <strong>1</strong> — Tiết kết thúc: <strong>5</strong></td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Thứ / Ngày nghỉ</th><td><strong>Thứ Sáu, ngày 20/11/2026</strong></td></tr>
                            <tr><th style="text-align: left; background-color: #f3f6fa;">Kế hoạch học bù</th><td>Giảng viên sẽ thông báo lịch học bù cụ thể trên hệ thống Thời khóa biểu vào tuần kế tiếp.</td></tr>
                        </tbody>
                    </table>
                    <p>Đề nghị sinh viên theo dõi thường xuyên trên Cổng thông tin Đào tạo để cập nhật lịch học bù.</p>
                    <p style="text-align: right;"><em>Trân trọng,<br />Phòng Đào tạo Đại học</em></p>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo học bù lớp Công nghệ Web và Ứng dụng ASP.NET (LHP005) ngày 15/11/2026",
                TomTat = "Thông báo lịch học bù môn Công nghệ Web và Ứng dụng ASP.NET (LHP005) tại phòng PM-01.",
                DanhMuc = "NghiBu",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Khoa Công nghệ Thông tin",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-1),
                XuatBanLuc = now.AddDays(-1),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Khoa Công nghệ Thông tin thông báo lịch học bù cho lớp học phần <strong>Công nghệ Web và Ứng dụng ASP.NET (LHP005)</strong> chi tiết như sau:</p>
                    <ul>
                        <li><strong>CBGD:</strong> TS. Nguyễn Văn An</li>
                        <li><strong>Khoa / Bộ môn:</strong> Khoa Công nghệ Thông tin</li>
                        <li><strong>Môn học:</strong> Công nghệ Web và Ứng dụng ASP.NET</li>
                        <li><strong>Lớp:</strong> LHP005 - Nhóm 01</li>
                        <li><strong>Phòng học bù:</strong> PM-01</li>
                        <li><strong>Thời gian:</strong> Tiết bắt đầu: 6 - Tiết kết thúc: 10</li>
                        <li><strong>Thứ / Ngày học bù:</strong> Thứ Bảy, ngày 15/11/2026</li>
                    </ul>
                    <p>Sinh viên lưu ý có mặt đúng giờ để điểm danh và thực hành trên phòng máy.</p>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo về việc thu học phí Học kỳ 1 năm học 2026 - 2027 hệ Đại học chính quy",
                TomTat = "Hướng dẫn thời gian, mức thu và phương thức thanh toán học phí trực tuyến cho sinh viên hệ Đại học chính quy.",
                DanhMuc = "HocPhi",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Phòng Kế hoạch - Tài chính",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-1).AddHours(-5),
                XuatBanLuc = now.AddDays(-1).AddHours(-5),
                GhimDauTrang = true,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <h3>1. Đối tượng và thời gian thu học phí</h3>
                    <p>Phòng Kế hoạch - Tài chính thông báo đến toàn thể sinh viên hệ Đại học chính quy về thời gian hoàn thành nghĩa vụ học phí Học kỳ 1 năm học 2026 - 2027:</p>
                    <ul>
                        <li><strong>Thời gian đóng học phí đợt 1:</strong> Từ ngày 01/11/2026 đến hết ngày 25/11/2026.</li>
                        <li><strong>Đơn giá tín chỉ chuẩn:</strong> 635.000 VNĐ / tín chỉ học phí.</li>
                    </ul>
                    <h3>2. Phương thức thanh toán</h3>
                    <p>Sinh viên tra cứu số tiền học phí tại mục <strong>Đăng ký học phần</strong> trên Cổng thông tin và thực hiện thanh toán qua cổng VietQR / Ngân hàng liên kết.</p>
                    """
            },
            new Article
            {
                TieuDe = "Kế hoạch đăng ký học phần và điều chỉnh Thời khóa biểu Học kỳ 2 năm học 2026 - 2027",
                TomTat = "Lịch trình đăng ký học phần trực tuyến, rút bớt học phần và xử lý trùng lịch cho sinh viên các khóa.",
                DanhMuc = "ThongBaoChung",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Phòng Đào tạo Đại học",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-2),
                XuatBanLuc = now.AddDays(-2),
                GhimDauTrang = true,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Phòng Đào tạo Đại học thông báo kế hoạch đăng ký học phần Học kỳ 2 năm học 2026 - 2027 trên hệ thống <strong>Academic Ledger</strong>:</p>
                    <ol>
                        <li><strong>Giai đoạn 1 (Đăng ký chính thức):</strong> Sinh viên đăng nhập vào phân hệ Đăng ký học phần để chọn các lớp mở trong học kỳ.</li>
                        <li><strong>Giai đoạn 2 (Hiệu chỉnh & Rút học phần):</strong> Sinh viên kiểm tra Thời khóa biểu cá nhân để tránh giao nhau tiết học.</li>
                    </ol>
                    <p>Mọi thắc mắc về mở thêm nhóm lớp, sinh viên liên hệ Văn phòng Khoa quản lý ngành học.</p>
                    """
            },
            new Article
            {
                TieuDe = "Hướng dẫn quy trình tra cứu điểm, xét điều kiện tốt nghiệp và gửi đơn phúc khảo trực tuyến",
                TomTat = "Tài liệu hướng dẫn chi tiết cách tra cứu bảng điểm tích lũy GPA và gửi yêu cầu phúc khảo bài thi trên hệ thống.",
                DanhMuc = "HuongDan",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Phòng Khảo thí & Đảm bảo Chất lượng",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-4),
                XuatBanLuc = now.AddDays(-4),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Nhằm hỗ trợ sinh viên theo dõi tiến độ học tập và thực hiện quyền phúc khảo điểm thi minh bạch, Phòng Khảo thí hướng dẫn quy trình gồm 3 bước:</p>
                    <ul>
                        <li><strong>Bước 1:</strong> Truy cập mục <em>Tra cứu điểm & Tốt nghiệp</em> để xem điểm Quá trình, Điểm thi kết thúc và Điểm tổng kết (thang 10, thang 4, điểm chữ).</li>
                        <li><strong>Bước 2:</strong> Nếu có thắc mắc về điểm số, chọn chức năng <em>Khiếu nại & Phúc khảo</em> trong vòng 07 ngày kể từ ngày công bố điểm.</li>
                        <li><strong>Bước 3:</strong> Theo dõi trạng thái xử lý đơn trực tiếp qua chuông thông báo của hệ thống.</li>
                    </ul>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo xét cấp Học bổng Khuyến khích học tập Học kỳ 1 năm học 2026 - 2027",
                TomTat = "Tiêu chí, mức học bổng loại Xuất sắc, Giỏi, Khá và danh sách dự kiến sinh viên đạt học bổng khuyến khích học tập.",
                DanhMuc = "HocBong",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Phòng Công tác Sinh viên",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-5),
                XuatBanLuc = now.AddDays(-5),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Phòng Công tác Sinh viên thông báo kế hoạch xét cấp <strong>Học bổng Khuyến khích học tập (KKHT)</strong> dựa trên kết quả học tập và điểm rèn luyện:</p>
                    <ul>
                        <li><strong>Học bổng loại Xuất sắc:</strong> GPA học kỳ từ 3.60 trở lên, điểm rèn luyện từ 90 điểm, không có môn dưới điểm B.</li>
                        <li><strong>Học bổng loại Giỏi:</strong> GPA học kỳ từ 3.20 trở lên, điểm rèn luyện từ 80 điểm.</li>
                        <li><strong>Học bổng loại Khá:</strong> GPA học kỳ từ 2.50 trở lên, điểm rèn luyện từ 70 điểm.</li>
                    </ul>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo đánh giá Điểm rèn luyện sinh viên và tuần sinh hoạt công dân đầu khóa",
                TomTat = "Lịch nộp phiếu tự đánh giá kết quả rèn luyện sinh viên và lịch học Tuần sinh hoạt công dân.",
                DanhMuc = "CTSV",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Phòng Công tác Sinh viên",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-6),
                XuatBanLuc = now.AddDays(-6),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Phòng Công tác Sinh viên đề nghị Ban cán sự các lớp hành chính triển khai họp lớp và chấm điểm rèn luyện học kỳ đúng tiến độ quy định.</p>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo tuyển sinh Chương trình Liên kết Quốc tế và Thạc sĩ Công nghệ Thông tin đợt 2 năm 2026",
                TomTat = "Thông tin chỉ tiêu, điều kiện xét tuyển và hồ sơ đăng ký dự tuyển chương trình Sau đại học & Liên kết quốc tế.",
                DanhMuc = "TuyenSinh",
                HeDaoTao = "Sau đại học",
                DonViDang = "Phòng Đào tạo Đại học",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-8),
                XuatBanLuc = now.AddDays(-8),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Nhà trường thông báo tuyển sinh đợt 2 năm 2026 các ngành Kỹ thuật Phần mềm, Hệ thống Thông tin và An toàn Thông tin với hình thức xét tuyển hồ sơ kết hợp phỏng vấn chuyên môn.</p>
                    """
            },
            new Article
            {
                TieuDe = "Thông báo ký phụ lục Hợp đồng đào tạo và thực tập doanh nghiệp cho sinh viên khóa K22",
                TomTat = "Hướng dẫn hoàn thiện hồ sơ hợp đồng thực tập tốt nghiệp và cam kết đào tạo tại các doanh nghiệp đối tác.",
                DanhMuc = "HopDong",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Khoa Công nghệ Thông tin",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-10),
                XuatBanLuc = now.AddDays(-10),
                GhimDauTrang = false,
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p>Sinh viên khóa K22 chuẩn bị bước vào học kỳ Thực tập doanh nghiệp vui lòng hoàn tất biểu mẫu Hợp đồng thực tập và nộp về Văn phòng Khoa trước ngày 30/11/2026.</p>
                    """
            },
            new Article
            {
                TieuDe = "Hội thảo Khoa học Sinh viên & Ngày hội Việc làm Công nghệ Academic TechDay 2026",
                TomTat = "Sự kiện thường niên quy tụ hơn 30 doanh nghiệp công nghệ hàng đầu cùng các đề tài nghiên cứu khoa học xuất sắc của sinh viên.",
                DanhMuc = "TinTuc",
                HeDaoTao = "Đại học chính quy",
                DonViDang = "Khoa Công nghệ Thông tin",
                NguoiDangId = author.UserID,
                NgayDang = now.AddDays(-2),
                XuatBanLuc = now.AddDays(-2),
                GhimDauTrang = false,
                ThumbnailUrl = "/images/portal-news.svg",
                TrangThai = "DaXuatBan",
                NoiDung = """
                    <p><strong>Academic TechDay 2026</strong> là cầu nối giữa sinh viên với các tập đoàn công nghệ, mang đến hơn 500 cơ hội thực tập và việc làm kỹ sư phần mềm, AI, dữ liệu và an toàn thông tin.</p>
                    """
            }
        };

        db.Articles.AddRange(sampleArticles);
        db.SaveChanges();
    }
}
