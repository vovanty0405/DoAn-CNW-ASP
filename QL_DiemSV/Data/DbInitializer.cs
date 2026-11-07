using Microsoft.EntityFrameworkCore;
using QL_DiemSV.Models;

namespace QL_DiemSV.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();
        AcademicSchemaUpgrade.Apply(context);

        if (context.Roles.Any())
        {
            return; // Database already seeded
        }

        // 1. Roles
        var roles = new List<Role>
        {
            new Role { RoleName = "Admin", MoTa = "Quản trị viên hệ thống, toàn quyền truy cập và cấu hình." },
            new Role { RoleName = "GiangVien", MoTa = "Giảng viên giảng dạy, nhập điểm quá trình, cuối kỳ và xử lý khiếu nại." },
            new Role { RoleName = "SinhVien", MoTa = "Sinh viên tra cứu kết quả học tập, tiến độ tốt nghiệp và gửi phúc khảo." },
            new Role { RoleName = "KhaoThi", MoTa = "Chuyên viên Phòng Khảo thí, nhập điểm thi lại và quản lý bảng điểm." }
        };
        context.Roles.AddRange(roles);
        context.SaveChanges();

        var adminRole = context.Roles.First(r => r.RoleName == "Admin");
        var gvRole = context.Roles.First(r => r.RoleName == "GiangVien");
        var svRole = context.Roles.First(r => r.RoleName == "SinhVien");
        var ktRole = context.Roles.First(r => r.RoleName == "KhaoThi");

        // 2. ChucNang & HanhDong
        var chucNangs = new List<ChucNang>
        {
            new ChucNang { MaChucNang = "TONG_QUAN", TenChucNang = "Trang chủ & Tổng quan", MoTa = "Xem thông báo và tổng quan hệ thống" },
            new ChucNang { MaChucNang = "THONG_KE", TenChucNang = "Thống kê & Báo cáo", MoTa = "Xem biểu đồ phân tích học tập" },
            new ChucNang { MaChucNang = "SINH_VIEN", TenChucNang = "Quản lý Sinh viên", MoTa = "Quản lý hồ sơ sinh viên" },
            new ChucNang { MaChucNang = "GIANG_VIEN", TenChucNang = "Quản lý Giảng viên", MoTa = "Quản lý hồ sơ giảng viên và Khoa" },
            new ChucNang { MaChucNang = "MON_HOC", TenChucNang = "Quản lý Môn học", MoTa = "Quản lý môn học và điều kiện tiên quyết" },
            new ChucNang { MaChucNang = "KHUNG_DAO_TAO", TenChucNang = "Khung chương trình đào tạo", MoTa = "Quản lý khung đào tạo và quy định tốt nghiệp" },
            new ChucNang { MaChucNang = "LOP_HOC_PHAN", TenChucNang = "Quản lý Lớp học phần", MoTa = "Quản lý lớp học phần, học kỳ và xếp lớp" },
            new ChucNang { MaChucNang = "LOP_HANH_CHINH", TenChucNang = "Quản lý Lớp hành chính", MoTa = "Quản lý lớp hành chính và ngành học" },
            new ChucNang { MaChucNang = "DIEM_SO", TenChucNang = "Quản lý Điểm số", MoTa = "Nhập điểm, cấu hình cột điểm và khóa điểm" },
            new ChucNang { MaChucNang = "TRA_CUU_DIEM", TenChucNang = "Tra cứu điểm & Tốt nghiệp", MoTa = "Tra cứu kết quả học tập và xét điều kiện tốt nghiệp" },
            new ChucNang { MaChucNang = "KHIEU_NAI", TenChucNang = "Khiếu nại & Phúc khảo", MoTa = "Gửi và xử lý đơn phúc khảo điểm" },
            new ChucNang { MaChucNang = "TAI_KHOAN", TenChucNang = "Tài khoản & Phân quyền", MoTa = "Quản lý người dùng và ma trận phân quyền" },
            new ChucNang { MaChucNang = "NHAT_KY", TenChucNang = "Nhật ký hệ thống", MoTa = "Theo dõi lịch sử thao tác hệ thống" }
        };
        context.ChucNangs.AddRange(chucNangs);

        var hanhDongs = new List<HanhDong>
        {
            new HanhDong { MaHanhDong = "VIEW", TenHanhDong = "Xem", MoTa = "Quyền xem dữ liệu" },
            new HanhDong { MaHanhDong = "CREATE", TenHanhDong = "Thêm mới", MoTa = "Quyền tạo mới bản ghi" },
            new HanhDong { MaHanhDong = "EDIT", TenHanhDong = "Cập nhật", MoTa = "Quyền chỉnh sửa bản ghi" },
            new HanhDong { MaHanhDong = "DELETE", TenHanhDong = "Xóa", MoTa = "Quyền xóa bản ghi" },
            new HanhDong { MaHanhDong = "IMPORT", TenHanhDong = "Nhập Excel", MoTa = "Quyền import dữ liệu hàng loạt" },
            new HanhDong { MaHanhDong = "EXPORT", TenHanhDong = "Xuất báo cáo", MoTa = "Quyền xuất file Excel/PDF" }
        };
        context.HanhDongs.AddRange(hanhDongs);
        context.SaveChanges();

        // 3. PhanQuyen
        var phanQuyens = new List<PhanQuyen>();
        // Admin has all permissions
        foreach (var cn in chucNangs)
        {
            foreach (var hd in hanhDongs)
            {
                phanQuyens.Add(new PhanQuyen { RoleID = adminRole.RoleID, MaChucNang = cn.MaChucNang, MaHanhDong = hd.MaHanhDong });
            }
        }
        // GiangVien permissions
        foreach (var cn in new[] { "TONG_QUAN", "THONG_KE", "LOP_HOC_PHAN", "DIEM_SO", "TRA_CUU_DIEM", "KHIEU_NAI" })
        {
            phanQuyens.Add(new PhanQuyen { RoleID = gvRole.RoleID, MaChucNang = cn, MaHanhDong = "VIEW" });
            if (cn == "DIEM_SO" || cn == "KHIEU_NAI")
            {
                phanQuyens.Add(new PhanQuyen { RoleID = gvRole.RoleID, MaChucNang = cn, MaHanhDong = "EDIT" });
                phanQuyens.Add(new PhanQuyen { RoleID = gvRole.RoleID, MaChucNang = cn, MaHanhDong = "IMPORT" });
                phanQuyens.Add(new PhanQuyen { RoleID = gvRole.RoleID, MaChucNang = cn, MaHanhDong = "EXPORT" });
            }
        }
        // SinhVien permissions
        foreach (var cn in new[] { "TONG_QUAN", "TRA_CUU_DIEM", "KHIEU_NAI" })
        {
            phanQuyens.Add(new PhanQuyen { RoleID = svRole.RoleID, MaChucNang = cn, MaHanhDong = "VIEW" });
            if (cn == "KHIEU_NAI")
            {
                phanQuyens.Add(new PhanQuyen { RoleID = svRole.RoleID, MaChucNang = cn, MaHanhDong = "CREATE" });
            }
        }
        // KhaoThi permissions
        foreach (var cn in new[] { "TONG_QUAN", "DIEM_SO", "TRA_CUU_DIEM" })
        {
            phanQuyens.Add(new PhanQuyen { RoleID = ktRole.RoleID, MaChucNang = cn, MaHanhDong = "VIEW" });
            if (cn == "DIEM_SO")
            {
                phanQuyens.Add(new PhanQuyen { RoleID = ktRole.RoleID, MaChucNang = cn, MaHanhDong = "EDIT" });
                phanQuyens.Add(new PhanQuyen { RoleID = ktRole.RoleID, MaChucNang = cn, MaHanhDong = "EXPORT" });
            }
        }
        context.PhanQuyens.AddRange(phanQuyens);

        // 4. PhanQuyenPhamVi
        context.PhanQuyenPhamVis.AddRange(
            new PhanQuyenPhamVi { RoleID = adminRole.RoleID, MaPhamVi = "scope_diem_all", Module = "Tra cứu điểm", Rule = "Xem toàn trường", IsActive = true },
            new PhanQuyenPhamVi { RoleID = adminRole.RoleID, MaPhamVi = "scope_thongke_all", Module = "Thống kê", Rule = "Thống kê toàn trường", IsActive = true },
            new PhanQuyenPhamVi { RoleID = gvRole.RoleID, MaPhamVi = "scope_diem_gv", Module = "Tra cứu điểm", Rule = "Chỉ xem sinh viên lớp mình phụ trách", IsActive = true },
            new PhanQuyenPhamVi { RoleID = gvRole.RoleID, MaPhamVi = "scope_thongke_canhan", Module = "Thống kê", Rule = "Thống kê cá nhân", IsActive = true },
            new PhanQuyenPhamVi { RoleID = svRole.RoleID, MaPhamVi = "scope_diem_canhan", Module = "Tra cứu điểm", Rule = "Chỉ xem điểm của cá nhân", IsActive = true }
        );
        context.SaveChanges();

        // 5. Khoa
        var khoas = new List<Khoa>
        {
            new Khoa { MaKhoa = "CNTT", TenKhoa = "Khoa Công nghệ Thông tin", NgayThanhLap = new DateTime(2005, 9, 1), TruongKhoa = "PGS.TS Nguyễn Văn An" },
            new Khoa { MaKhoa = "KT", TenKhoa = "Khoa Kinh tế & Quản trị", NgayThanhLap = new DateTime(2006, 5, 15), TruongKhoa = "TS. Trần Thị Bích" },
            new Khoa { MaKhoa = "DTVT", TenKhoa = "Khoa Điện tử Viễn thông", NgayThanhLap = new DateTime(2007, 10, 20), TruongKhoa = "TS. Lê Hoàng Nam" },
            new Khoa { MaKhoa = "NN", TenKhoa = "Khoa Ngoại ngữ", NgayThanhLap = new DateTime(2008, 3, 12), TruongKhoa = "ThS. Phạm Thu Hương" }
        };
        context.Khoas.AddRange(khoas);
        context.SaveChanges();

        // 6. Nganh & QuyDinhTotNghiep
        var nganhs = new List<Nganh>
        {
            new Nganh { MaNganh = "CNPM", TenNganh = "Kỹ thuật Phần mềm", MaKhoa = "CNTT" },
            new Nganh { MaNganh = "HTTT", TenNganh = "Hệ thống Thông tin", MaKhoa = "CNTT" },
            new Nganh { MaNganh = "ATTT", TenNganh = "An toàn Thông tin", MaKhoa = "CNTT" },
            new Nganh { MaNganh = "QTKD", TenNganh = "Quản trị Kinh doanh", MaKhoa = "KT" },
            new Nganh { MaNganh = "DTVT01", TenNganh = "Kỹ thuật Điện tử Viễn thông", MaKhoa = "DTVT" }
        };
        context.Nganhs.AddRange(nganhs);
        context.SaveChanges();

        // 7. MonHoc & DieuKienMonHoc
        var monHocs = new List<MonHoc>
        {
            new MonHoc { MaMon = "IT001", TenMon = "Nhập môn Lập trình", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT002", TenMon = "Lập trình Hướng đối tượng", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT003", TenMon = "Cấu trúc Dữ liệu và Giải thuật", SoTinChi = 4, SoTietLyThuyet = 45, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT004", TenMon = "Cơ sở Dữ liệu", SoTinChi = 4, SoTietLyThuyet = 45, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT005", TenMon = "Công nghệ Web và Ứng dụng ASP.NET", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT006", TenMon = "Phát triển Ứng dụng Di động", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT007", TenMon = "Trí tuệ Nhân tạo", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "IT008", TenMon = "An toàn và Bảo mật Hệ thống", SoTinChi = 3, SoTietLyThuyet = 30, SoTietThucHanh = 15, MaKhoa = "CNTT" },
            new MonHoc { MaMon = "EC001", TenMon = "Kinh tế Vi mô", SoTinChi = 3, SoTietLyThuyet = 45, SoTietThucHanh = 0, MaKhoa = "KT" },
            new MonHoc { MaMon = "EN001", TenMon = "Tiếng Anh Chuyên ngành CNTT", SoTinChi = 3, SoTietLyThuyet = 45, SoTietThucHanh = 0, MaKhoa = "NN" }
        };
        context.MonHocs.AddRange(monHocs);
        context.SaveChanges();

        context.DieuKienMonHocs.AddRange(
            new DieuKienMonHoc { MaMon = "IT002", MaMonTienQuyet = "IT001" },
            new DieuKienMonHoc { MaMon = "IT003", MaMonTienQuyet = "IT001" },
            new DieuKienMonHoc { MaMon = "IT005", MaMonTienQuyet = "IT002" },
            new DieuKienMonHoc { MaMon = "IT005", MaMonTienQuyet = "IT004" }
        );
        context.SaveChanges();

        // 8. KhungChuongTrinh
        var kctList = new List<KhungChuongTrinh>
        {
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT001", HocKyDuKien = 1, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT002", HocKyDuKien = 2, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT003", HocKyDuKien = 2, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT004", HocKyDuKien = 3, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT005", HocKyDuKien = 4, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT006", HocKyDuKien = 5, LoaiMon = "Tự chọn" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "IT007", HocKyDuKien = 5, LoaiMon = "Tự chọn" },
            new KhungChuongTrinh { MaNganh = "CNPM", MaMon = "EN001", HocKyDuKien = 3, LoaiMon = "Bắt buộc" },

            new KhungChuongTrinh { MaNganh = "HTTT", MaMon = "IT001", HocKyDuKien = 1, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "HTTT", MaMon = "IT002", HocKyDuKien = 2, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "HTTT", MaMon = "IT004", HocKyDuKien = 2, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "HTTT", MaMon = "IT005", HocKyDuKien = 3, LoaiMon = "Bắt buộc" },
            new KhungChuongTrinh { MaNganh = "HTTT", MaMon = "IT008", HocKyDuKien = 4, LoaiMon = "Tự chọn" }
        };
        context.KhungChuongTrinhs.AddRange(kctList);
        context.SaveChanges();

        foreach (var n in nganhs)
        {
            var khungOfMajor = kctList.Where(k => k.MaNganh == n.MaNganh).ToList();
            int tcBB = khungOfMajor.Where(k => k.LoaiMon == "Bắt buộc")
                .Sum(k => monHocs.FirstOrDefault(m => m.MaMon == k.MaMon)?.SoTinChi ?? 0);
            int tcTC = khungOfMajor.Where(k => k.LoaiMon != "Bắt buộc")
                .Sum(k => monHocs.FirstOrDefault(m => m.MaMon == k.MaMon)?.SoTinChi ?? 0);
            int tongTC = tcBB + tcTC;

            context.QuyDinhTotNghieps.Add(new QuyDinhTotNghiep
            {
                MaNganh = n.MaNganh,
                TongTTC = tongTC > 0 ? tongTC : 130,
                TCBatBuoc = tongTC > 0 ? tcBB : 112,
                TCTuChon = tongTC > 0 ? tcTC : 18,
                MinGPA = 2.0
            });
        }
        context.SaveChanges();

        // 9. HocKy
        var hocKys = new List<HocKy>
        {
            new HocKy { MaHK = "HK1_2324", TenHK = "Học kỳ 1", NamHocBatDau = 2023, NamHocKetThuc = 2024 },
            new HocKy { MaHK = "HK2_2324", TenHK = "Học kỳ 2", NamHocBatDau = 2023, NamHocKetThuc = 2024 },
            new HocKy { MaHK = "HK1_2425", TenHK = "Học kỳ 1", NamHocBatDau = 2024, NamHocKetThuc = 2025 },
            new HocKy { MaHK = "HK2_2425", TenHK = "Học kỳ 2", NamHocBatDau = 2024, NamHocKetThuc = 2025 }
        };
        context.HocKys.AddRange(hocKys);
        context.SaveChanges();

        // 10. UserAccounts & GiangViens
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");
        string adminHash = BCrypt.Net.BCrypt.HashPassword("admin123");

        var adminAccount = new UserAccount
        {
            Username = "admin",
            PasswordHash = adminHash,
            RoleID = adminRole.RoleID,
            IsActive = true,
            NgayTao = DateTime.Now
        };
        var khaoThiAccount = new UserAccount
        {
            Username = "khaothi",
            PasswordHash = defaultHash,
            RoleID = ktRole.RoleID,
            IsActive = true,
            NgayTao = DateTime.Now
        };
        context.UserAccounts.AddRange(adminAccount, khaoThiAccount);
        context.SaveChanges();

        var gvData = new[]
        {
            new { MaGV = "GV001", HoTen = "TS. Nguyễn Văn An", GioiTinh = "Nam", NgaySinh = new DateTime(1980, 5, 12), HocVi = "Tiến sĩ", MaKhoa = "CNTT", Email = "annv@edu.vn", SDT = "0901234561", CCCD = "079080001234" },
            new { MaGV = "GV002", HoTen = "ThS. Trần Thị Minh", GioiTinh = "Nữ", NgaySinh = new DateTime(1985, 8, 22), HocVi = "Thạc sĩ", MaKhoa = "CNTT", Email = "minhtt@edu.vn", SDT = "0901234562", CCCD = "079185001235" },
            new { MaGV = "GV003", HoTen = "TS. Lê Hoàng Cường", GioiTinh = "Nam", NgaySinh = new DateTime(1979, 11, 3), HocVi = "Tiến sĩ", MaKhoa = "CNTT", Email = "cuonglh@edu.vn", SDT = "0901234563", CCCD = "079079001236" },
            new { MaGV = "GV004", HoTen = "ThS. Phạm Quốc Bảo", GioiTinh = "Nam", NgaySinh = new DateTime(1988, 2, 18), HocVi = "Thạc sĩ", MaKhoa = "KT", Email = "baopq@edu.vn", SDT = "0901234564", CCCD = "079088001237" },
            new { MaGV = "GV005", HoTen = "ThS. Vũ Thu Thảo", GioiTinh = "Nữ", NgaySinh = new DateTime(1990, 7, 29), HocVi = "Thạc sĩ", MaKhoa = "NN", Email = "thaovt@edu.vn", SDT = "0901234565", CCCD = "079190001238" }
        };

        foreach (var g in gvData)
        {
            var ua = new UserAccount
            {
                Username = g.MaGV,
                PasswordHash = defaultHash,
                RoleID = gvRole.RoleID,
                IsActive = true,
                MaGV = g.MaGV,
                NgayTao = DateTime.Now
            };
            context.UserAccounts.Add(ua);
            context.SaveChanges();

            context.GiangViens.Add(new GiangVien
            {
                MaGV = g.MaGV,
                HoTen = g.HoTen,
                GioiTinh = g.GioiTinh,
                NgaySinh = g.NgaySinh,
                HocVi = g.HocVi,
                MaKhoa = g.MaKhoa,
                Email = g.Email,
                SDT = g.SDT,
                CCCD = g.CCCD,
                UserID = ua.UserID
            });
            context.SaveChanges();
        }

        // 11. LopHanhChinh
        var lopHanhChinhs = new List<LopHanhChinh>
        {
            new LopHanhChinh { MaLop = "D22CQCN01", TenLop = "Công nghệ phần mềm 01 - K22", NienKhoa = "2022-2026", MaNganh = "CNPM", MaGVCN = "GV001" },
            new LopHanhChinh { MaLop = "D22CQCN02", TenLop = "Công nghệ phần mềm 02 - K22", NienKhoa = "2022-2026", MaNganh = "CNPM", MaGVCN = "GV002" },
            new LopHanhChinh { MaLop = "D22CQHT01", TenLop = "Hệ thống thông tin 01 - K22", NienKhoa = "2022-2026", MaNganh = "HTTT", MaGVCN = "GV003" },
            new LopHanhChinh { MaLop = "D23CQKD01", TenLop = "Quản trị kinh doanh 01 - K23", NienKhoa = "2023-2027", MaNganh = "QTKD", MaGVCN = "GV004" }
        };
        context.LopHanhChinhs.AddRange(lopHanhChinhs);
        context.SaveChanges();

        // 12. SinhVien & UserAccount
        var svData = new[]
        {
            new { MaSV = "SV001", HoTen = "Nguyễn Minh Quân", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 3, 15), MaLop = "D22CQCN01", TrangThai = "Đang học", DiaChi = "Quận 1, TP.HCM" },
            new { MaSV = "SV002", HoTen = "Trần Ngọc Ánh", GioiTinh = "Nữ", NgaySinh = new DateTime(2004, 6, 20), MaLop = "D22CQCN01", TrangThai = "Đang học", DiaChi = "Quận 3, TP.HCM" },
            new { MaSV = "SV003", HoTen = "Lê Văn Hùng", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 1, 10), MaLop = "D22CQCN01", TrangThai = "Đang học", DiaChi = "TP. Thủ Đức, TP.HCM" },
            new { MaSV = "SV004", HoTen = "Phạm Thị Mai", GioiTinh = "Nữ", NgaySinh = new DateTime(2004, 9, 5), MaLop = "D22CQCN01", TrangThai = "Đang học", DiaChi = "Quận Bình Thạnh, TP.HCM" },
            new { MaSV = "SV005", HoTen = "Hoàng Gia Bảo", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 12, 1), MaLop = "D22CQCN01", TrangThai = "Đang học", DiaChi = "Quận Gò Vấp, TP.HCM" },
            new { MaSV = "SV006", HoTen = "Đặng Thu Thủy", GioiTinh = "Nữ", NgaySinh = new DateTime(2004, 4, 18), MaLop = "D22CQCN02", TrangThai = "Đang học", DiaChi = "Quận 7, TP.HCM" },
            new { MaSV = "SV007", HoTen = "Bùi Tuấn Kiệt", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 7, 25), MaLop = "D22CQCN02", TrangThai = "Đang học", DiaChi = "Quận Tân Bình, TP.HCM" },
            new { MaSV = "SV008", HoTen = "Đỗ Khánh Linh", GioiTinh = "Nữ", NgaySinh = new DateTime(2004, 11, 12), MaLop = "D22CQCN02", TrangThai = "Đang học", DiaChi = "Quận 10, TP.HCM" },
            new { MaSV = "SV009", HoTen = "Ngô Đức Trọng", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 2, 28), MaLop = "D22CQHT01", TrangThai = "Đang học", DiaChi = "Quận Phú Nhuận, TP.HCM" },
            new { MaSV = "SV010", HoTen = "Võ Thanh Trúc", GioiTinh = "Nữ", NgaySinh = new DateTime(2004, 8, 14), MaLop = "D22CQHT01", TrangThai = "Đang học", DiaChi = "Quận 12, TP.HCM" },
            new { MaSV = "SV011", HoTen = "Lý Hoàng Long", GioiTinh = "Nam", NgaySinh = new DateTime(2004, 5, 9), MaLop = "D22CQHT01", TrangThai = "Bảo lưu", DiaChi = "Bình Dương" },
            new { MaSV = "SV012", HoTen = "Trương Mỹ Duyên", GioiTinh = "Nữ", NgaySinh = new DateTime(2005, 10, 22), MaLop = "D23CQKD01", TrangThai = "Đang học", DiaChi = "Đồng Nai" }
        };

        int svIdx = 1;
        foreach (var s in svData)
        {
            var ua = new UserAccount
            {
                Username = s.MaSV,
                PasswordHash = defaultHash,
                RoleID = svRole.RoleID,
                IsActive = true,
                NgayTao = DateTime.Now
            };
            context.UserAccounts.Add(ua);
            context.SaveChanges();

            context.SinhViens.Add(new SinhVien
            {
                MaSV = s.MaSV,
                HoTen = s.HoTen,
                GioiTinh = s.GioiTinh,
                NgaySinh = s.NgaySinh,
                MaLop = s.MaLop,
                TrangThai = s.TrangThai,
                DiaChi = s.DiaChi,
                Email = $"{s.MaSV.ToLower()}@student.edu.vn",
                SDT = $"09876543{svIdx:D2}",
                CCCD = $"0792040000{svIdx:D2}",
                UserID = ua.UserID
            });
            context.SaveChanges();
            svIdx++;
        }

        // 13. LopHocPhan
        string defaultCauHinh = "[{\"id\":\"col_cc\",\"name\":\"Chuyên cần\",\"weight\":25},{\"id\":\"col_gk\",\"name\":\"Giữa kỳ\",\"weight\":75}]";
        var lopHocPhans = new List<LopHocPhan>
        {
            new LopHocPhan { MaLHP = "LHP001", TenLopHP = "Nhập môn Lập trình - Nhóm 01", MaMon = "IT001", MaHK = "HK1_2324", MaGV = "GV001", PhongHoc = "A101", SiSoToiDa = 40, TrangThai = "Đã chốt", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Đã khóa" },
            new LopHocPhan { MaLHP = "LHP002", TenLopHP = "Lập trình Hướng đối tượng - Nhóm 01", MaMon = "IT002", MaHK = "HK2_2324", MaGV = "GV001", PhongHoc = "A102", SiSoToiDa = 40, TrangThai = "Đã chốt", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Đã khóa" },
            new LopHocPhan { MaLHP = "LHP003", TenLopHP = "Cấu trúc Dữ liệu & Giải thuật - Nhóm 01", MaMon = "IT003", MaHK = "HK2_2324", MaGV = "GV003", PhongHoc = "B201", SiSoToiDa = 40, TrangThai = "Đã chốt", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Đang nhập" },
            new LopHocPhan { MaLHP = "LHP004", TenLopHP = "Cơ sở Dữ liệu - Nhóm 01", MaMon = "IT004", MaHK = "HK1_2425", MaGV = "GV002", PhongHoc = "B203", SiSoToiDa = 40, TrangThai = "MoDangKy", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Đang nhập" },
            new LopHocPhan { MaLHP = "LHP005", TenLopHP = "Công nghệ Web ASP.NET - Nhóm 01", MaMon = "IT005", MaHK = "HK2_2425", MaGV = "GV001", PhongHoc = "PM-01", SiSoToiDa = 35, TrangThai = "MoDangKy", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Đang nhập" },
            new LopHocPhan { MaLHP = "LHP006", TenLopHP = "Phát triển Ứng dụng Di động - Nhóm 01", MaMon = "IT006", MaHK = "HK2_2425", MaGV = "GV002", PhongHoc = "PM-02", SiSoToiDa = 35, TrangThai = "MoDangKy", CauHinhDiem = defaultCauHinh, TrangThaiNhapDiem = "Chờ mở khóa" }
        };
        foreach (var section in lopHocPhans)
        {
            section.STC = context.MonHocs.Find(section.MaMon)?.SoTinChi ?? 0;
            section.STCHP = section.STC;
        }
        context.LopHocPhans.AddRange(lopHocPhans);
        context.SaveChanges();

        // 14. KetQuaHocTap
        var kqList = new List<KetQuaHocTap>
        {
            // SV001
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP001", DiemCC = 9.0, DiemGK = 8.5, DiemCK = 8.6, DiemThiLan1 = 8.5, DiemTongKet = 8.5, DiemChu = "A", ChiTietDiemQT = "{\"col_cc\":9.0,\"col_gk\":8.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP002", DiemCC = 8.5, DiemGK = 8.0, DiemCK = 8.1, DiemThiLan1 = 8.0, DiemTongKet = 8.0, DiemChu = "B+", ChiTietDiemQT = "{\"col_cc\":8.5,\"col_gk\":8.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP003", DiemCC = 8.0, DiemGK = 7.5, DiemCK = 7.6, DiemThiLan1 = 7.5, DiemTongKet = 7.5, DiemChu = "B", ChiTietDiemQT = "{\"col_cc\":8.0,\"col_gk\":7.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP004", DiemCC = 9.0, DiemGK = 8.5, DiemCK = 8.6, DiemThiLan1 = 9.0, DiemTongKet = 8.8, DiemChu = "A", ChiTietDiemQT = "{\"col_cc\":9.0,\"col_gk\":8.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP005", DiemCC = 9.5, DiemGK = 9.0, DiemCK = 9.1, DiemThiLan1 = 9.2, DiemTongKet = 9.2, DiemChu = "A+", ChiTietDiemQT = "{\"col_cc\":9.5,\"col_gk\":9.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV001", MaLHP = "LHP006", DiemCC = 8.5, DiemGK = 8.0, DiemCK = 8.1, DiemThiLan1 = 8.2, DiemTongKet = 8.2, DiemChu = "B+", ChiTietDiemQT = "{\"col_cc\":8.5,\"col_gk\":8.0}", GhiChu = "Đạt" },

            // SV002
            new KetQuaHocTap { MaSV = "SV002", MaLHP = "LHP001", DiemCC = 8.0, DiemGK = 7.0, DiemCK = 7.3, DiemThiLan1 = 7.5, DiemTongKet = 7.4, DiemChu = "B", ChiTietDiemQT = "{\"col_cc\":8.0,\"col_gk\":7.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV002", MaLHP = "LHP002", DiemCC = 7.5, DiemGK = 6.5, DiemCK = 6.8, DiemThiLan1 = 7.0, DiemTongKet = 6.9, DiemChu = "C+", ChiTietDiemQT = "{\"col_cc\":7.5,\"col_gk\":6.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV002", MaLHP = "LHP004", DiemCC = 8.5, DiemGK = 8.0, DiemCK = 8.1, DiemThiLan1 = 8.0, DiemTongKet = 8.0, DiemChu = "B+", ChiTietDiemQT = "{\"col_cc\":8.5,\"col_gk\":8.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV002", MaLHP = "LHP005", DiemCC = 8.0, DiemGK = 7.5, DiemCK = 7.6, DiemThiLan1 = 8.0, DiemTongKet = 7.8, DiemChu = "B", ChiTietDiemQT = "{\"col_cc\":8.0,\"col_gk\":7.5}", GhiChu = "Đạt" },

            // SV003 (có môn thi lại)
            new KetQuaHocTap { MaSV = "SV003", MaLHP = "LHP001", DiemCC = 6.0, DiemGK = 5.0, DiemCK = 5.3, DiemThiLan1 = 3.0, DiemThiLan2 = 6.5, DiemTongKet = 6.0, DiemChu = "C", ChiTietDiemQT = "{\"col_cc\":6.0,\"col_gk\":5.0}", GhiChu = "Đạt (Thi lại)" },
            new KetQuaHocTap { MaSV = "SV003", MaLHP = "LHP002", DiemCC = 6.5, DiemGK = 5.5, DiemCK = 5.8, DiemThiLan1 = 5.5, DiemTongKet = 5.6, DiemChu = "C", ChiTietDiemQT = "{\"col_cc\":6.5,\"col_gk\":5.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV003", MaLHP = "LHP005", DiemCC = 7.0, DiemGK = 6.0, DiemCK = 6.3, DiemThiLan1 = 6.5, DiemTongKet = 6.4, DiemChu = "C", ChiTietDiemQT = "{\"col_cc\":7.0,\"col_gk\":6.0}", GhiChu = "Đạt" },

            // SV004
            new KetQuaHocTap { MaSV = "SV004", MaLHP = "LHP001", DiemCC = 5.0, DiemGK = 4.0, DiemCK = 4.3, DiemThiLan1 = 3.2, DiemTongKet = 3.6, DiemChu = "F", ChiTietDiemQT = "{\"col_cc\":5.0,\"col_gk\":4.0}", GhiChu = "Học lại" },
            new KetQuaHocTap { MaSV = "SV004", MaLHP = "LHP004", DiemCC = 7.5, DiemGK = 7.0, DiemCK = 7.1, DiemThiLan1 = 7.2, DiemTongKet = 7.2, DiemChu = "B", ChiTietDiemQT = "{\"col_cc\":7.5,\"col_gk\":7.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV004", MaLHP = "LHP005", DiemCC = 8.0, DiemGK = 7.5, DiemCK = 7.6, DiemThiLan1 = 7.5, DiemTongKet = 7.5, DiemChu = "B", ChiTietDiemQT = "{\"col_cc\":8.0,\"col_gk\":7.5}", GhiChu = "Đạt" },

            // SV005..SV008
            new KetQuaHocTap { MaSV = "SV005", MaLHP = "LHP005", DiemCC = 8.5, DiemGK = 8.0, DiemCK = 8.1, DiemThiLan1 = 8.5, DiemTongKet = 8.3, DiemChu = "B+", ChiTietDiemQT = "{\"col_cc\":8.5,\"col_gk\":8.0}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV006", MaLHP = "LHP004", DiemCC = 9.0, DiemGK = 8.5, DiemCK = 8.6, DiemThiLan1 = 8.8, DiemTongKet = 8.7, DiemChu = "A", ChiTietDiemQT = "{\"col_cc\":9.0,\"col_gk\":8.5}", GhiChu = "Đạt" },
            new KetQuaHocTap { MaSV = "SV007", MaLHP = "LHP004", DiemCC = 6.5, DiemGK = 6.0, DiemCK = 6.1, DiemThiLan1 = 6.0, DiemTongKet = 6.0, DiemChu = "C", ChiTietDiemQT = "{\"col_cc\":6.5,\"col_gk\":6.0}", GhiChu = "Đạt" }
        };
        context.KetQuaHocTaps.AddRange(kqList);
        context.SaveChanges();

        // 15. DonKhieuNai
        context.DonKhieuNais.AddRange(
            new DonKhieuNai
            {
                MaSV = "SV002",
                MaLHP = "LHP005",
                LoaiDiem = "thiLan1",
                LyDo = "Em xin phúc khảo lại bài thi cuối kỳ môn Công nghệ Web ASP.NET vì em làm đầy đủ 4 câu tự luận.",
                NgayGui = DateTime.Now.AddDays(-2),
                TrangThai = "pending",
                DaXem = false
            },
            new DonKhieuNai
            {
                MaSV = "SV003",
                MaLHP = "LHP002",
                LoaiDiem = "quatrinh",
                LyDo = "Thầy kiểm tra lại giúp em cột điểm giữa kỳ, em có nộp bài bổ sung đúng hạn.",
                NgayGui = DateTime.Now.AddDays(-5),
                TrangThai = "approved",
                PhanHoi = "Đã kiểm tra bài nộp bổ sung và cập nhật điểm quá trình.",
                DaXem = true
            }
        );
        context.SaveChanges();

        // 16. ThongBao
        context.ThongBaos.AddRange(
            new ThongBao
            {
                MaNguoiGui = "SV002",
                MaNguoiNhan = "GV001",
                TieuDe = "Khiếu nại điểm mới",
                NoiDung = "Sinh viên Trần Ngọc Ánh (SV002) đã gửi khiếu nại điểm môn Công nghệ Web ASP.NET - Nhóm 01.",
                LoaiThongBao = "COMPLAINT_NEW",
                ThamChieuID = "1",
                NgayGui = DateTime.Now.AddDays(-2),
                DaDoc = false
            },
            new ThongBao
            {
                MaNguoiGui = "GV002",
                MaNguoiNhan = "ADMIN",
                TieuDe = "Yêu cầu mở khóa điểm LHP006",
                NoiDung = "Giảng viên GV002 yêu cầu mở khóa điểm cho lớp LHP006 (Phát triển Ứng dụng Di động - Nhóm 01).",
                LoaiThongBao = "UNLOCK_REQUEST",
                ThamChieuID = "LHP006",
                NgayGui = DateTime.Now.AddDays(-1),
                DaDoc = false
            },
            new ThongBao
            {
                MaNguoiGui = "GV001",
                MaNguoiNhan = "SV003",
                TieuDe = "Kết quả khiếu nại điểm",
                NoiDung = "Đơn khiếu nại môn Lập trình Hướng đối tượng của bạn đã được Chấp nhận. Phản hồi: Đã kiểm tra bài nộp bổ sung và cập nhật điểm quá trình.",
                LoaiThongBao = "COMPLAINT_PROCESSED",
                ThamChieuID = "2",
                NgayGui = DateTime.Now.AddDays(-4),
                DaDoc = false
            }
        );

        // 17. NhatKyHoatDong
        context.NhatKyHoatDongs.AddRange(
            new NhatKyHoatDong { NguoiDung = "admin", HanhDong = "Khởi tạo hệ thống", ChiTiet = "Khởi tạo cơ sở dữ liệu Quản lý điểm sinh viên ASP.NET Core MVC", Loai = "Thông tin", ThoiGian = DateTime.Now.AddDays(-7) },
            new NhatKyHoatDong { NguoiDung = "GV001", HanhDong = "Chốt điểm lớp học phần", ChiTiet = "Giảng viên GV001 đã chốt sổ điểm lớp LHP001", Loai = "Thông tin", ThoiGian = DateTime.Now.AddDays(-3) },
            new NhatKyHoatDong { NguoiDung = "GV002", HanhDong = "Xin mở khóa điểm", ChiTiet = "Giảng viên GV002 gửi yêu cầu mở khóa bảng điểm lớp LHP006", Loai = "Cảnh báo", ThoiGian = DateTime.Now.AddDays(-1) }
        );
        context.SaveChanges();
    }
}
