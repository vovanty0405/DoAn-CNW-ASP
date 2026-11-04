-- Academic Ledger: complete SQL Server schema, generated from ApplicationDbContext.
-- Includes tables, columns, identity keys, primary/foreign keys and indexes.
-- No sample records, passwords, or existing user data are exported.
-- Run in SQL Server Management Studio. This script only accepts an empty database.
-- To use another database name, change QuanLyDiemSV in CREATE DATABASE and USE below.
-- Existing populated databases: use the application's upgrade mechanism instead.
USE [master];
GO
IF DB_ID(N'QuanLyDiemSV') IS NULL
    EXEC(N'CREATE DATABASE [QuanLyDiemSV]');
GO
USE [QuanLyDiemSV];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
BEGIN
    THROW 51000, N'Database already contains tables. Select an empty database; existing data has not been deleted.', 1;
END
ELSE
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
-- 28 tables: dbo.Article, dbo.ChucNang, dbo.ClassSchedule, dbo.Curriculums, dbo.CurriculumSections, dbo.DieuKienMonHoc, dbo.DonKhieuNai, dbo.GiangVien, dbo.HanhDong, dbo.HocKy, dbo.KetQuaHocTap, dbo.Khoa, dbo.KhungChuongTrinh, dbo.LopHanhChinh, dbo.LopHocPhan, dbo.MonHoc, dbo.Nganh, dbo.NhatKyHoatDong, dbo.PhanQuyen, dbo.PhanQuyenPhamVi, dbo.PhongHoc, dbo.PortalDocument, dbo.QuyDinhTotNghiep, dbo.Roles, dbo.Sessions, dbo.SinhVien, dbo.ThongBao, dbo.UserAccounts
CREATE TABLE [ChucNang] (
    [MaChucNang] nvarchar(50) NOT NULL,
    [TenChucNang] nvarchar(100) NOT NULL,
    [MoTa] nvarchar(255) NULL,
    CONSTRAINT [PK_ChucNang] PRIMARY KEY ([MaChucNang])
);

CREATE TABLE [HanhDong] (
    [MaHanhDong] nvarchar(50) NOT NULL,
    [TenHanhDong] nvarchar(100) NOT NULL,
    [MoTa] nvarchar(255) NULL,
    CONSTRAINT [PK_HanhDong] PRIMARY KEY ([MaHanhDong])
);

CREATE TABLE [HocKy] (
    [MaHK] nvarchar(20) NOT NULL,
    [TenHK] nvarchar(50) NOT NULL,
    [NamHocBatDau] int NOT NULL,
    [NamHocKetThuc] int NOT NULL,
    [NgayBatDauTuan1] datetime2 NULL,
    [BatDauDangKy] datetime2 NULL,
    [KetThucDangKy] datetime2 NULL,
    [SoTuan] int NOT NULL,
    [DonGiaTinChi] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_HocKy] PRIMARY KEY ([MaHK])
);

CREATE TABLE [Khoa] (
    [MaKhoa] nvarchar(20) NOT NULL,
    [TenKhoa] nvarchar(255) NOT NULL,
    [NgayThanhLap] date NULL,
    [TruongKhoa] nvarchar(100) NULL,
    CONSTRAINT [PK_Khoa] PRIMARY KEY ([MaKhoa])
);

CREATE TABLE [NhatKyHoatDong] (
    [MaLog] int NOT NULL IDENTITY,
    [NguoiDung] nvarchar(100) NULL,
    [ThoiGian] datetime2 NOT NULL,
    [HanhDong] nvarchar(255) NULL,
    [ChiTiet] nvarchar(max) NULL,
    [Loai] nvarchar(50) NULL,
    CONSTRAINT [PK_NhatKyHoatDong] PRIMARY KEY ([MaLog])
);

CREATE TABLE [PhongHoc] (
    [MaPhong] nvarchar(50) NOT NULL,
    [ToaNha] nvarchar(30) NOT NULL,
    [TrangThai] nvarchar(20) NOT NULL,
    [SucChua] int NOT NULL,
    CONSTRAINT [PK_PhongHoc] PRIMARY KEY ([MaPhong])
);

CREATE TABLE [PortalDocument] (
    [Id] int NOT NULL IDENTITY,
    [TieuDe] nvarchar(250) NOT NULL,
    [Loai] nvarchar(30) NOT NULL,
    [FileUrl] nvarchar(500) NOT NULL,
    [DungLuongBytes] bigint NOT NULL,
    [HienThi] bit NOT NULL,
    CONSTRAINT [PK_PortalDocument] PRIMARY KEY ([Id])
);

CREATE TABLE [Roles] (
    [RoleID] int NOT NULL IDENTITY,
    [RoleName] nvarchar(50) NOT NULL,
    [MoTa] nvarchar(255) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleID])
);

CREATE TABLE [ThongBao] (
    [MaThongBao] int NOT NULL IDENTITY,
    [MaNguoiGui] nvarchar(20) NULL,
    [MaNguoiNhan] nvarchar(20) NULL,
    [TieuDe] nvarchar(255) NULL,
    [NoiDung] nvarchar(max) NULL,
    [LoaiThongBao] nvarchar(50) NULL,
    [ThamChieuID] nvarchar(50) NULL,
    [NgayGui] datetime2 NOT NULL,
    [DaDoc] bit NOT NULL,
    CONSTRAINT [PK_ThongBao] PRIMARY KEY ([MaThongBao])
);

CREATE TABLE [MonHoc] (
    [MaMon] nvarchar(20) NOT NULL,
    [TenMon] nvarchar(255) NOT NULL,
    [SoTinChi] int NOT NULL,
    [SoTietLyThuyet] int NULL,
    [SoTietThucHanh] int NULL,
    [MaKhoa] nvarchar(20) NULL,
    CONSTRAINT [PK_MonHoc] PRIMARY KEY ([MaMon]),
    CONSTRAINT [FK_MonHoc_Khoa_MaKhoa] FOREIGN KEY ([MaKhoa]) REFERENCES [Khoa] ([MaKhoa])
);

CREATE TABLE [Nganh] (
    [MaNganh] nvarchar(20) NOT NULL,
    [TenNganh] nvarchar(255) NOT NULL,
    [HeDaoTao] nvarchar(50) NOT NULL,
    [MaKhoa] nvarchar(20) NULL,
    CONSTRAINT [PK_Nganh] PRIMARY KEY ([MaNganh]),
    CONSTRAINT [FK_Nganh_Khoa_MaKhoa] FOREIGN KEY ([MaKhoa]) REFERENCES [Khoa] ([MaKhoa])
);

CREATE TABLE [PhanQuyen] (
    [RoleID] int NOT NULL,
    [MaChucNang] nvarchar(50) NOT NULL,
    [MaHanhDong] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_PhanQuyen] PRIMARY KEY ([RoleID], [MaChucNang], [MaHanhDong]),
    CONSTRAINT [FK_PhanQuyen_ChucNang_MaChucNang] FOREIGN KEY ([MaChucNang]) REFERENCES [ChucNang] ([MaChucNang]) ON DELETE CASCADE,
    CONSTRAINT [FK_PhanQuyen_HanhDong_MaHanhDong] FOREIGN KEY ([MaHanhDong]) REFERENCES [HanhDong] ([MaHanhDong]) ON DELETE CASCADE,
    CONSTRAINT [FK_PhanQuyen_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE
);

CREATE TABLE [PhanQuyenPhamVi] (
    [RoleID] int NOT NULL,
    [MaPhamVi] nvarchar(50) NOT NULL,
    [Module] nvarchar(100) NOT NULL,
    [Rule] nvarchar(255) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_PhanQuyenPhamVi] PRIMARY KEY ([RoleID], [MaPhamVi]),
    CONSTRAINT [FK_PhanQuyenPhamVi_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE
);

CREATE TABLE [UserAccounts] (
    [UserID] int NOT NULL IDENTITY,
    [Username] nvarchar(50) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [RoleID] int NULL,
    [IsActive] bit NOT NULL,
    [NgayTao] datetime2 NOT NULL,
    [MaGV] nvarchar(20) NULL,
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([UserID]),
    CONSTRAINT [FK_UserAccounts_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID])
);

CREATE TABLE [DieuKienMonHoc] (
    [ID] int NOT NULL IDENTITY,
    [MaMon] nvarchar(20) NULL,
    [MaMonTienQuyet] nvarchar(20) NULL,
    CONSTRAINT [PK_DieuKienMonHoc] PRIMARY KEY ([ID]),
    CONSTRAINT [FK_DieuKienMonHoc_MonHoc_MaMon] FOREIGN KEY ([MaMon]) REFERENCES [MonHoc] ([MaMon]) ON DELETE CASCADE,
    CONSTRAINT [FK_DieuKienMonHoc_MonHoc_MaMonTienQuyet] FOREIGN KEY ([MaMonTienQuyet]) REFERENCES [MonHoc] ([MaMon]) ON DELETE NO ACTION
);

CREATE TABLE [Curriculums] (
    [Id] int NOT NULL IDENTITY,
    [HeDaoTao] nvarchar(80) NOT NULL,
    [KhoaHoc] int NOT NULL,
    [NganhHocId] nvarchar(20) NOT NULL,
    [TenChuongTrinh] nvarchar(200) NOT NULL,
    [TrangThai] bit NOT NULL,
    [NgayTao] datetime2 NOT NULL,
    [NgayCapNhat] datetime2 NOT NULL,
    CONSTRAINT [PK_Curriculums] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Curriculums_Nganh_NganhHocId] FOREIGN KEY ([NganhHocId]) REFERENCES [Nganh] ([MaNganh]) ON DELETE NO ACTION
);

CREATE TABLE [KhungChuongTrinh] (
    [ID] int NOT NULL IDENTITY,
    [MaNganh] nvarchar(20) NOT NULL,
    [MaMon] nvarchar(20) NOT NULL,
    [HocKyDuKien] int NOT NULL,
    [LoaiMon] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_KhungChuongTrinh] PRIMARY KEY ([ID]),
    CONSTRAINT [FK_KhungChuongTrinh_MonHoc_MaMon] FOREIGN KEY ([MaMon]) REFERENCES [MonHoc] ([MaMon]) ON DELETE CASCADE,
    CONSTRAINT [FK_KhungChuongTrinh_Nganh_MaNganh] FOREIGN KEY ([MaNganh]) REFERENCES [Nganh] ([MaNganh]) ON DELETE CASCADE
);

CREATE TABLE [QuyDinhTotNghiep] (
    [MaNganh] nvarchar(20) NOT NULL,
    [TongTTC] int NOT NULL,
    [TCBatBuoc] int NOT NULL,
    [TCTuChon] int NOT NULL,
    [MinGPA] float NOT NULL,
    CONSTRAINT [PK_QuyDinhTotNghiep] PRIMARY KEY ([MaNganh]),
    CONSTRAINT [FK_QuyDinhTotNghiep_Nganh_MaNganh] FOREIGN KEY ([MaNganh]) REFERENCES [Nganh] ([MaNganh]) ON DELETE CASCADE
);

CREATE TABLE [Article] (
    [Id] int NOT NULL IDENTITY,
    [TieuDe] nvarchar(250) NOT NULL,
    [TomTat] nvarchar(1000) NOT NULL,
    [NoiDung] nvarchar(max) NOT NULL,
    [DanhMuc] nvarchar(40) NOT NULL,
    [HeDaoTao] nvarchar(100) NOT NULL,
    [DonViDang] nvarchar(200) NOT NULL,
    [NguoiDangId] int NOT NULL,
    [NgayDang] datetime2 NOT NULL,
    [GhimDauTrang] bit NOT NULL,
    [FileDinhKemUrl] nvarchar(500) NULL,
    [TenFileDinhKem] nvarchar(255) NULL,
    [DungLuongFile] bigint NULL,
    [ThumbnailUrl] nvarchar(500) NULL,
    [TrangThai] nvarchar(20) NOT NULL,
    [XuatBanLuc] datetime2 NULL,
    CONSTRAINT [PK_Article] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Article_UserAccounts_NguoiDangId] FOREIGN KEY ([NguoiDangId]) REFERENCES [UserAccounts] ([UserID]) ON DELETE NO ACTION
);

CREATE TABLE [GiangVien] (
    [MaGV] nvarchar(20) NOT NULL,
    [HoTen] nvarchar(255) NOT NULL,
    [NgaySinh] date NULL,
    [GioiTinh] nvarchar(10) NULL,
    [Email] nvarchar(255) NULL,
    [SDT] nvarchar(20) NULL,
    [HocVi] nvarchar(100) NULL,
    [MaKhoa] nvarchar(20) NULL,
    [UserID] int NULL,
    [CCCD] nvarchar(20) NULL,
    CONSTRAINT [PK_GiangVien] PRIMARY KEY ([MaGV]),
    CONSTRAINT [FK_GiangVien_Khoa_MaKhoa] FOREIGN KEY ([MaKhoa]) REFERENCES [Khoa] ([MaKhoa]),
    CONSTRAINT [FK_GiangVien_UserAccounts_UserID] FOREIGN KEY ([UserID]) REFERENCES [UserAccounts] ([UserID]) ON DELETE SET NULL
);

CREATE TABLE [Sessions] (
    [SessionID] int NOT NULL IDENTITY,
    [UserID] int NOT NULL,
    [RefreshToken] nvarchar(max) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsRevoked] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Sessions] PRIMARY KEY ([SessionID]),
    CONSTRAINT [FK_Sessions_UserAccounts_UserID] FOREIGN KEY ([UserID]) REFERENCES [UserAccounts] ([UserID]) ON DELETE CASCADE
);

CREATE TABLE [CurriculumSections] (
    [Id] int NOT NULL IDENTITY,
    [CurriculumId] int NOT NULL,
    [ThuTu] int NOT NULL,
    [TieuDeMuc] nvarchar(250) NOT NULL,
    [NoiDung] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CurriculumSections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CurriculumSections_Curriculums_CurriculumId] FOREIGN KEY ([CurriculumId]) REFERENCES [Curriculums] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [LopHanhChinh] (
    [MaLop] nvarchar(20) NOT NULL,
    [TenLop] nvarchar(255) NOT NULL,
    [NienKhoa] nvarchar(50) NULL,
    [MaNganh] nvarchar(20) NULL,
    [MaGVCN] nvarchar(20) NULL,
    CONSTRAINT [PK_LopHanhChinh] PRIMARY KEY ([MaLop]),
    CONSTRAINT [FK_LopHanhChinh_GiangVien_MaGVCN] FOREIGN KEY ([MaGVCN]) REFERENCES [GiangVien] ([MaGV]) ON DELETE SET NULL,
    CONSTRAINT [FK_LopHanhChinh_Nganh_MaNganh] FOREIGN KEY ([MaNganh]) REFERENCES [Nganh] ([MaNganh])
);

CREATE TABLE [LopHocPhan] (
    [MaLHP] nvarchar(20) NOT NULL,
    [MaMon] nvarchar(20) NULL,
    [MaHK] nvarchar(20) NULL,
    [MaGV] nvarchar(20) NULL,
    [TenLopHP] nvarchar(255) NULL,
    [PhongHoc] nvarchar(50) NULL,
    [SiSoToiDa] int NULL,
    [NMH] nvarchar(10) NOT NULL,
    [TTH] nvarchar(10) NULL,
    [STC] int NOT NULL,
    [STCHP] decimal(8,2) NOT NULL,
    [MaLop] nvarchar(20) NULL,
    [TrangThai] nvarchar(50) NULL,
    [CauHinhDiem] nvarchar(max) NULL,
    [TrangThaiNhapDiem] nvarchar(50) NULL,
    [TyLeQuaTrinh] int NOT NULL,
    [TyLeThiKetThuc] int NOT NULL,
    CONSTRAINT [PK_LopHocPhan] PRIMARY KEY ([MaLHP]),
    CONSTRAINT [FK_LopHocPhan_GiangVien_MaGV] FOREIGN KEY ([MaGV]) REFERENCES [GiangVien] ([MaGV]) ON DELETE SET NULL,
    CONSTRAINT [FK_LopHocPhan_HocKy_MaHK] FOREIGN KEY ([MaHK]) REFERENCES [HocKy] ([MaHK]),
    CONSTRAINT [FK_LopHocPhan_MonHoc_MaMon] FOREIGN KEY ([MaMon]) REFERENCES [MonHoc] ([MaMon])
);

CREATE TABLE [SinhVien] (
    [MaSV] nvarchar(20) NOT NULL,
    [HoTen] nvarchar(255) NOT NULL,
    [NgaySinh] date NULL,
    [GioiTinh] nvarchar(10) NULL,
    [DiaChi] nvarchar(255) NULL,
    [CCCD] nvarchar(20) NULL,
    [Email] nvarchar(255) NULL,
    [SDT] nvarchar(20) NULL,
    [MaLop] nvarchar(20) NULL,
    [TrangThai] nvarchar(50) NULL,
    [UserID] int NULL,
    CONSTRAINT [PK_SinhVien] PRIMARY KEY ([MaSV]),
    CONSTRAINT [FK_SinhVien_LopHanhChinh_MaLop] FOREIGN KEY ([MaLop]) REFERENCES [LopHanhChinh] ([MaLop]),
    CONSTRAINT [FK_SinhVien_UserAccounts_UserID] FOREIGN KEY ([UserID]) REFERENCES [UserAccounts] ([UserID]) ON DELETE SET NULL
);

CREATE TABLE [ClassSchedule] (
    [Id] int NOT NULL IDENTITY,
    [MaLHP] nvarchar(20) NOT NULL,
    [LoaiLich] nvarchar(20) NOT NULL,
    [LoaiBuoiHoc] bit NOT NULL,
    [Thu] int NOT NULL,
    [TietBatDau] int NOT NULL,
    [SoTiet] int NOT NULL,
    [PhongHoc] nvarchar(50) NOT NULL,
    [MaGV] nvarchar(20) NOT NULL,
    [TuanBaoNghi] nvarchar(200) NOT NULL,
    [ChuoiTuanHoc] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_ClassSchedule] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClassSchedule_GiangVien_MaGV] FOREIGN KEY ([MaGV]) REFERENCES [GiangVien] ([MaGV]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ClassSchedule_LopHocPhan_MaLHP] FOREIGN KEY ([MaLHP]) REFERENCES [LopHocPhan] ([MaLHP]) ON DELETE CASCADE,
    CONSTRAINT [FK_ClassSchedule_PhongHoc_PhongHoc] FOREIGN KEY ([PhongHoc]) REFERENCES [PhongHoc] ([MaPhong]) ON DELETE NO ACTION
);

CREATE TABLE [DonKhieuNai] (
    [MaKN] int NOT NULL IDENTITY,
    [MaSV] nvarchar(20) NOT NULL,
    [MaLHP] nvarchar(20) NOT NULL,
    [LyDo] nvarchar(max) NOT NULL,
    [LoaiDiem] nvarchar(50) NOT NULL,
    [NgayGui] datetime2 NOT NULL,
    [TrangThai] nvarchar(50) NOT NULL,
    [PhanHoi] nvarchar(max) NULL,
    [DaXem] bit NOT NULL,
    CONSTRAINT [PK_DonKhieuNai] PRIMARY KEY ([MaKN]),
    CONSTRAINT [FK_DonKhieuNai_LopHocPhan_MaLHP] FOREIGN KEY ([MaLHP]) REFERENCES [LopHocPhan] ([MaLHP]) ON DELETE CASCADE,
    CONSTRAINT [FK_DonKhieuNai_SinhVien_MaSV] FOREIGN KEY ([MaSV]) REFERENCES [SinhVien] ([MaSV]) ON DELETE CASCADE
);

CREATE TABLE [KetQuaHocTap] (
    [MaKQ] int NOT NULL IDENTITY,
    [MaSV] nvarchar(20) NULL,
    [MaLHP] nvarchar(20) NULL,
    [HocPhiDangKy] decimal(18,2) NULL,
    [MienGiam] decimal(18,2) NOT NULL,
    [NgayDangKy] datetime2 NULL,
    [DiemCC] float NULL,
    [DiemGK] float NULL,
    [DiemCK] float NULL,
    [DiemChu] nvarchar(5) NULL,
    [GhiChu] nvarchar(255) NULL,
    [DiemThiLan1] float NULL,
    [DiemThiLan2] float NULL,
    [DiemTongKet] float NULL,
    [ChiTietDiemQT] nvarchar(max) NULL,
    CONSTRAINT [PK_KetQuaHocTap] PRIMARY KEY ([MaKQ]),
    CONSTRAINT [FK_KetQuaHocTap_LopHocPhan_MaLHP] FOREIGN KEY ([MaLHP]) REFERENCES [LopHocPhan] ([MaLHP]) ON DELETE CASCADE,
    CONSTRAINT [FK_KetQuaHocTap_SinhVien_MaSV] FOREIGN KEY ([MaSV]) REFERENCES [SinhVien] ([MaSV]) ON DELETE CASCADE
);

CREATE INDEX [IX_Article_NguoiDangId] ON [Article] ([NguoiDangId]);

CREATE INDEX [IX_Article_TrangThai_DanhMuc_NgayDang] ON [Article] ([TrangThai], [DanhMuc], [NgayDang]);

CREATE INDEX [IX_Article_TrangThai_GhimDauTrang_NgayDang_Id] ON [Article] ([TrangThai], [GhimDauTrang] DESC, [NgayDang] DESC, [Id] DESC);

CREATE INDEX [IX_ClassSchedule_MaGV] ON [ClassSchedule] ([MaGV]);

CREATE INDEX [IX_ClassSchedule_MaLHP_Thu] ON [ClassSchedule] ([MaLHP], [Thu]);

CREATE INDEX [IX_ClassSchedule_PhongHoc] ON [ClassSchedule] ([PhongHoc]);

CREATE UNIQUE INDEX [IX_Curriculums_HeDaoTao_KhoaHoc_NganhHocId_TenChuongTrinh] ON [Curriculums] ([HeDaoTao], [KhoaHoc], [NganhHocId], [TenChuongTrinh]);

CREATE INDEX [IX_Curriculums_NganhHocId] ON [Curriculums] ([NganhHocId]);

CREATE UNIQUE INDEX [IX_CurriculumSections_CurriculumId_ThuTu] ON [CurriculumSections] ([CurriculumId], [ThuTu]);

CREATE INDEX [IX_DieuKienMonHoc_MaMon] ON [DieuKienMonHoc] ([MaMon]);

CREATE INDEX [IX_DieuKienMonHoc_MaMonTienQuyet] ON [DieuKienMonHoc] ([MaMonTienQuyet]);

CREATE INDEX [IX_DonKhieuNai_MaLHP] ON [DonKhieuNai] ([MaLHP]);

CREATE INDEX [IX_DonKhieuNai_MaSV] ON [DonKhieuNai] ([MaSV]);

CREATE INDEX [IX_GiangVien_MaKhoa] ON [GiangVien] ([MaKhoa]);

CREATE UNIQUE INDEX [IX_GiangVien_UserID] ON [GiangVien] ([UserID]) WHERE [UserID] IS NOT NULL;

CREATE INDEX [IX_KetQuaHocTap_MaLHP_DiemTongKet] ON [KetQuaHocTap] ([MaLHP], [DiemTongKet]);

CREATE INDEX [IX_KetQuaHocTap_MaSV_MaLHP] ON [KetQuaHocTap] ([MaSV], [MaLHP]);

CREATE INDEX [IX_KhungChuongTrinh_MaMon] ON [KhungChuongTrinh] ([MaMon]);

CREATE INDEX [IX_KhungChuongTrinh_MaNganh] ON [KhungChuongTrinh] ([MaNganh]);

CREATE UNIQUE INDEX [IX_KhungChuongTrinh_MaNganh_MaMon] ON [KhungChuongTrinh] ([MaNganh], [MaMon]);

CREATE INDEX [IX_LopHanhChinh_MaGVCN] ON [LopHanhChinh] ([MaGVCN]);

CREATE INDEX [IX_LopHanhChinh_MaNganh] ON [LopHanhChinh] ([MaNganh]);

CREATE INDEX [IX_LopHocPhan_MaGV] ON [LopHocPhan] ([MaGV]);

CREATE INDEX [IX_LopHocPhan_MaHK] ON [LopHocPhan] ([MaHK]);

CREATE INDEX [IX_LopHocPhan_MaMon] ON [LopHocPhan] ([MaMon]);

CREATE INDEX [IX_MonHoc_MaKhoa] ON [MonHoc] ([MaKhoa]);

CREATE INDEX [IX_Nganh_MaKhoa] ON [Nganh] ([MaKhoa]);

CREATE INDEX [IX_PhanQuyen_MaChucNang] ON [PhanQuyen] ([MaChucNang]);

CREATE INDEX [IX_PhanQuyen_MaHanhDong] ON [PhanQuyen] ([MaHanhDong]);

CREATE INDEX [IX_Sessions_UserID] ON [Sessions] ([UserID]);

CREATE INDEX [IX_SinhVien_MaLop] ON [SinhVien] ([MaLop]);

CREATE UNIQUE INDEX [IX_SinhVien_UserID] ON [SinhVien] ([UserID]) WHERE [UserID] IS NOT NULL;

CREATE INDEX [IX_UserAccounts_RoleID] ON [UserAccounts] ([RoleID]);

CREATE UNIQUE INDEX [IX_UserAccounts_Username] ON [UserAccounts] ([Username]);
        COMMIT TRANSACTION;
        PRINT N'Academic Ledger schema created successfully.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
SELECT s.name AS [Schema], t.name AS [Table]
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0 ORDER BY s.name, t.name;
GO