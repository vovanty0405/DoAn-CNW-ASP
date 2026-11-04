-- Academic Ledger synthetic seed. Run AFTER Database_QLDiemSV_Full.sql on EMPTY tables.
-- Uses IDENTITY defaults + SCOPE_IDENTITY; no explicit identity values or IDENTITY_INSERT needed.
-- Default test passwords: admin/admin123; all other accounts/123456 (BCrypt hashes stored below).
-- NEVER use these demo credentials in production. No real persons are represented.
USE [QuanLyDiemSV];
GO
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.[ChucNang]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[HanhDong]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[HocKy]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Khoa]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[NhatKyHoatDong]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[PhongHoc]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[PortalDocument]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Roles]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[ThongBao]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[MonHoc]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Nganh]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[PhanQuyen]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[PhanQuyenPhamVi]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[UserAccounts]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[DieuKienMonHoc]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Curriculums]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[KhungChuongTrinh]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[QuyDinhTotNghiep]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Article]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[GiangVien]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[Sessions]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[CurriculumSections]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[LopHanhChinh]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[LopHocPhan]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[SinhVien]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[ClassSchedule]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[DonKhieuNai]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
IF EXISTS(SELECT 1 FROM dbo.[KetQuaHocTap]) THROW 51000,N'Seed requires empty tables; existing data is not modified.',1;
INSERT Roles(RoleName,MoTa) VALUES(N'Admin',N'Quản trị'),(N'GiangVien',N'Giảng viên'),(N'SinhVien',N'Sinh viên'),(N'GiaoVuKhoa',N'Giáo vụ');
INSERT Khoa(MaKhoa,TenKhoa,TruongKhoa) VALUES(N'CNTT',N'Khoa Công nghệ Thông tin',N'TS. Nguyễn Văn An'),(N'SP',N'Khoa Sư phạm',N'PGS.TS. Trần Văn Minh'),(N'KTQT',N'Khoa Kinh tế - Quản trị kinh doanh',N'TS. Lê Thị Hương'),(N'NN',N'Khoa Ngoại ngữ',N'TS. Phạm Quốc Tuấn');
CREATE TABLE #Majors(Id int PRIMARY KEY,Code nvarchar(20),Title nvarchar(255),Faculty nvarchar(20),Prefix nvarchar(3),ClassSuffix nvarchar(2));
INSERT #Majors VALUES(1,N'CNTT',N'Công nghệ thông tin',N'CNTT',N'DTH',N'TH'),(2,N'KTPM',N'Kỹ thuật phần mềm',N'CNTT',N'DPM',N'PM'),(3,N'KHMT',N'Khoa học máy tính',N'CNTT',N'DKH',N'KH'),(4,N'SPTOAN',N'Sư phạm Toán học',N'SP',N'DTO',N'TO'),(5,N'SPAV',N'Sư phạm Tiếng Anh',N'SP',N'DTA',N'TA'),(6,N'SPVL',N'Sư phạm Vật lý',N'SP',N'DLY',N'LY'),(7,N'QTKD',N'Quản trị kinh doanh',N'KTQT',N'',N''),(8,N'TCNH',N'Tài chính Ngân hàng',N'KTQT',N'',N''),(9,N'KT',N'Kế toán',N'KTQT',N'',N''),(10,N'NNA',N'Ngôn ngữ Anh',N'NN',N'',N''),(11,N'NNT',N'Ngôn ngữ Trung Quốc',N'NN',N'',N''),(12,N'NNP',N'Ngôn ngữ Pháp',N'NN',N'',N'');
INSERT Nganh(MaNganh,TenNganh,MaKhoa,HeDaoTao) SELECT Code,Title,Faculty,N'Đại học chính quy' FROM #Majors;
INSERT QuyDinhTotNghiep SELECT Code,140,112,28,2.0 FROM #Majors;
CREATE TABLE #Courses(Code nvarchar(20) PRIMARY KEY,Title nvarchar(255),TC int,Faculty nvarchar(20),Sem int,Scope nvarchar(20));
INSERT #Courses VALUES(N'PHI104',N'Triết học Mác - Lênin',3,N'CNTT',1,N'ALL'),(N'MAX309',N'Kinh tế chính trị Mác - Lênin',2,N'CNTT',1,N'ALL'),(N'MAX310',N'Chủ nghĩa xã hội khoa học',2,N'CNTT',1,N'ALL'),(N'VRP505',N'Lịch sử Đảng Cộng sản Việt Nam',2,N'CNTT',1,N'ALL'),(N'HCM101',N'Tư tưởng Hồ Chí Minh',2,N'CNTT',1,N'ALL'),(N'LAW101',N'Pháp luật đại cương',2,N'CNTT',1,N'ALL'),(N'ENG110',N'Tiếng Anh 1',4,N'CNTT',1,N'ALL'),(N'ENG111',N'Tiếng Anh 2',4,N'CNTT',1,N'ALL'),(N'ENG302',N'Tiếng Anh 3',4,N'CNTT',1,N'ALL'),(N'COS101',N'Tin học đại cương',3,N'CNTT',1,N'ALL'),(N'COS104',N'Giới thiệu ngành CNTT',1,N'CNTT',1,N'IT'),(N'SEE101',N'Giới thiệu ngành KTPM',1,N'CNTT',1,N'IT'),(N'MAT104',N'Toán A1',3,N'CNTT',1,N'IT'),(N'PHY118',N'Vật lý cho tin học',4,N'CNTT',1,N'IT'),(N'MAT113',N'Toán rời rạc',3,N'CNTT',1,N'IT'),(N'COS106',N'Lập trình căn bản',4,N'CNTT',1,N'IT'),(N'CON301',N'Mạng máy tính',2,N'CNTT',1,N'IT'),(N'MAT105',N'Toán A2',3,N'CNTT',2,N'IT'),(N'COS108',N'Ngôn ngữ lập trình Java',3,N'CNTT',2,N'IT'),(N'COS330',N'Kiến trúc máy tính và hợp ngữ',3,N'CNTT',2,N'IT'),(N'COS304',N'Cấu trúc dữ liệu',3,N'CNTT',2,N'IT'),(N'COS303',N'Phương pháp tính CNTT',2,N'CNTT',2,N'IT'),(N'COS107',N'Nền tảng CNTT',2,N'CNTT',2,N'IT'),(N'MAT106',N'Toán A3',3,N'CNTT',3,N'IT'),(N'COS310',N'Nguyên lý hệ điều hành',3,N'CNTT',3,N'IT'),(N'COS311',N'Cơ sở dữ liệu',3,N'CNTT',3,N'IT'),(N'COS313',N'Phương pháp lập trình hướng đối tượng',3,N'CNTT',3,N'IT'),(N'COS309',N'Phân tích và thiết kế giải thuật',3,N'CNTT',3,N'IT'),(N'SEE301',N'Nhập môn công nghệ phần mềm',2,N'CNTT',3,N'IT'),(N'PRS302',N'Xác suất thống kê A - CNTT',3,N'CNTT',4,N'IT'),(N'COS503',N'Lý thuyết đồ thị',3,N'CNTT',4,N'IT'),(N'CON503',N'Quản trị mạng',3,N'CNTT',4,N'IT'),(N'IMS301',N'Nguyên lý hệ quản trị CSDL',3,N'CNTT',4,N'IT'),(N'COS504',N'CSDL nâng cao',3,N'CNTT',4,N'IT'),(N'SEE910',N'Điện toán đám mây',3,N'CNTT',4,N'IT'),(N'SEE527',N'Phát triển ứng dụng Web',4,N'CNTT',5,N'IT'),(N'TIE501',N'Lập trình .Net',4,N'CNTT',5,N'IT'),(N'IMS302',N'Phân tích thiết kế HTTT',3,N'CNTT',5,N'IT'),(N'SEE529',N'Phân tích và thiết kế phần mềm',4,N'CNTT',5,N'IT'),(N'COS508',N'Xử lý ảnh',3,N'CNTT',5,N'IT'),(N'COS525',N'Chuyên đề Python',3,N'CNTT',5,N'IT'),(N'IMS912',N'Chuyên đề Java',3,N'CNTT',5,N'IT'),(N'MOR514',N'Phương pháp NCKH - CNTT',3,N'CNTT',6,N'IT'),(N'IMS501',N'Lập trình quản lý',3,N'CNTT',6,N'IT'),(N'CON915',N'Thiết kế và cài đặt mạng',2,N'CNTT',6,N'IT'),(N'SEE505',N'PTTK phần mềm hướng đối tượng',3,N'CNTT',6,N'IT'),(N'SEE530',N'Kiến trúc và mẫu thiết kế phần mềm',3,N'CNTT',6,N'IT'),(N'SEE512',N'Kiểm thử và đảm bảo chất lượng PM',3,N'CNTT',6,N'IT'),(N'CON502',N'Lập trình thiết bị di động',3,N'CNTT',6,N'IT'),(N'COS529',N'Thị giác máy tính',3,N'CNTT',6,N'IT'),(N'COS521',N'Trí tuệ nhân tạo',3,N'CNTT',7,N'IT'),(N'COS526',N'Quản lý dự án CNTT',3,N'CNTT',7,N'IT'),(N'SEE528',N'Công nghệ Web và ứng dụng',3,N'CNTT',7,N'IT'),(N'CON511',N'An toàn hệ thống và an ninh mạng',3,N'CNTT',7,N'IT'),(N'COS527',N'Công nghệ Blockchain',3,N'CNTT',7,N'IT'),(N'SEE531',N'Triển khai và bảo trì phần mềm',3,N'CNTT',7,N'IT'),(N'COS528',N'Trực quan hóa dữ liệu',3,N'CNTT',7,N'IT'),(N'TIE903',N'Thực tập cuối khóa CNTT',5,N'CNTT',8,N'IT'),(N'TIE913',N'Khóa luận tốt nghiệp CNTT',10,N'CNTT',8,N'IT'),(N'SEE919',N'Thực tập tốt nghiệp KTPM',5,N'CNTT',8,N'IT'),(N'SEE920',N'Khóa luận tốt nghiệp KTPM',10,N'CNTT',8,N'IT'),(N'COS915',N'Trí tuệ nhân tạo nâng cao',3,N'CNTT',8,N'IT'),(N'COS916',N'Khai thác dữ liệu lớn',3,N'CNTT',8,N'IT'),(N'COS917',N'Khai phá dữ liệu',3,N'CNTT',8,N'IT'),(N'PED117',N'Giáo dục học',3,N'SP',3,N'SP'),(N'PSY109',N'Tâm lý học sư phạm',4,N'SP',3,N'SP'),(N'EDU103',N'Quản lý hành chính NN và GDĐT',2,N'SP',3,N'SP'),(N'PED317',N'Thực hành nghề nghiệp 1',2,N'SP',3,N'SP'),(N'PED318',N'Thực hành nghề nghiệp 2',2,N'SP',3,N'SP'),(N'EDU126',N'Giới thiệu ngành SP Toán',2,N'SP',7,N'SPTOAN'),(N'ALG115',N'Đại số tuyến tính',4,N'SP',1,N'SPTOAN'),(N'CAL111',N'Giải tích thực một biến',4,N'SP',2,N'SPTOAN'),(N'ALG114',N'Đại số sơ cấp',4,N'SP',3,N'SPTOAN'),(N'ALG116',N'Đại số đại cương',3,N'SP',4,N'SPTOAN'),(N'CAL112',N'Giải tích thực nhiều biến',4,N'SP',5,N'SPTOAN'),(N'GEM111',N'Hình học sơ cấp',4,N'SP',6,N'SPTOAN'),(N'PED338',N'Lý luận dạy học môn Toán',4,N'SP',7,N'SPTOAN'),(N'CAL505',N'Phương trình vi phân',3,N'SP',1,N'SPTOAN'),(N'ALG512',N'Lý thuyết số',3,N'SP',2,N'SPTOAN'),(N'PRS303',N'Xác suất và thống kê SP Toán',4,N'SP',3,N'SPTOAN'),(N'EDU512',N'Thực tập sư phạm 1',3,N'SP',4,N'SPTOAN'),(N'EDU812',N'Thực tập sư phạm 2',5,N'SP',5,N'SPTOAN'),(N'EDU945',N'Khóa luận tốt nghiệp SP Toán',10,N'SP',6,N'SPTOAN'),(N'EDU134',N'Giới thiệu ngành SP TA',2,N'SP',7,N'SPAV'),(N'LAS113',N'Ngữ pháp cơ bản',3,N'SP',1,N'SPAV'),(N'LAS501',N'Ngữ âm thực hành cơ bản',2,N'SP',2,N'SPAV'),(N'GSK106',N'Nghe & Nói A2',4,N'SP',3,N'SPAV'),(N'GSK107',N'Đọc & Viết A2',4,N'SP',4,N'SPAV'),(N'LAS115',N'Ngữ pháp nâng cao',3,N'SP',5,N'SPAV'),(N'GSK108',N'Nghe & Nói B1',4,N'SP',6,N'SPAV'),(N'GSK311',N'Nghe & Nói B2.1',3,N'SP',7,N'SPAV'),(N'PED583',N'Nhập môn PPGD Tiếng Anh',3,N'SP',1,N'SPAV'),(N'LAS525',N'Ngữ âm học & Âm vị học',2,N'SP',2,N'SPAV'),(N'LAS533',N'Cú pháp & Hình thái học',3,N'SP',3,N'SPAV'),(N'EDU519',N'Thực tập sư phạm 1 SP TA',3,N'SP',4,N'SPAV'),(N'EDU819',N'Thực tập sư phạm 2 SP TA',5,N'SP',5,N'SPAV'),(N'EDU952',N'Khóa luận tốt nghiệp SP TA',10,N'SP',6,N'SPAV'),(N'PHY115',N'Nhập môn SP Vật lý',2,N'SP',7,N'SPVL'),(N'MAT111',N'Toán A1 - SP VL',3,N'SP',1,N'SPVL'),(N'PHY116',N'Cơ học',5,N'SP',2,N'SPVL'),(N'PHY114',N'Nhiệt học',4,N'SP',3,N'SPVL'),(N'TEC111',N'Điện từ học',4,N'SP',4,N'SPVL'),(N'PHY117',N'Thí nghiệm Vật lí 1',2,N'SP',5,N'SPVL'),(N'PED545',N'Lý luận dạy học Vật lí',3,N'SP',6,N'SPVL'),(N'PHY309',N'Dao động và sóng',4,N'SP',7,N'SPVL'),(N'PHY304',N'Quang học',4,N'SP',1,N'SPVL'),(N'PHY509',N'Cơ học lý thuyết',3,N'SP',2,N'SPVL'),(N'PHY546',N'Cơ học lượng tử',4,N'SP',3,N'SPVL'),(N'PHY514',N'Vật lí nguyên tử hạt nhân',3,N'SP',4,N'SPVL'),(N'EDU518',N'Thực tập sư phạm 1 SP VL',3,N'SP',5,N'SPVL'),(N'EDU813',N'Thực tập sư phạm 2 SP VL',5,N'SP',6,N'SPVL'),(N'EDU946',N'Khóa luận tốt nghiệp SP VL',10,N'SP',7,N'SPVL');
INSERT MonHoc(MaMon,TenMon,SoTinChi,SoTietLyThuyet,SoTietThucHanh,MaKhoa) SELECT Code,Title,TC,TC*15,0,Faculty FROM #Courses;
UPDATE MonHoc SET SoTietLyThuyet=15,SoTietThucHanh=30 WHERE MaMon='COS101';
UPDATE MonHoc SET SoTietLyThuyet=30,SoTietThucHanh=60 WHERE MaMon IN('COS106','SEE527','TIE501');
UPDATE MonHoc SET SoTietLyThuyet=15,SoTietThucHanh=60 WHERE MaMon='COS108';
UPDATE MonHoc SET SoTietLyThuyet=45,SoTietThucHanh=30 WHERE MaMon='PHY118';
INSERT DieuKienMonHoc(MaMon,MaMonTienQuyet) VALUES(N'ENG111',N'ENG110'),(N'ENG302',N'ENG111'),(N'MAT105',N'MAT104'),(N'COS108',N'COS106'),(N'COS304',N'COS106'),(N'COS310',N'COS330'),(N'COS313',N'COS106'),(N'COS309',N'COS304'),(N'COS503',N'COS304'),(N'CON503',N'CON301'),(N'IMS301',N'COS311'),(N'IMS302',N'COS311'),(N'IMS501',N'IMS302'),(N'CON915',N'CON301'),(N'COS526',N'SEE301'),(N'SEE528',N'SEE527'),(N'CON511',N'CON503'),(N'ALG116',N'ALG115'),(N'CAL112',N'CAL111'),(N'LAS115',N'LAS113'),(N'PHY309',N'PHY116'),(N'MAX309',N'PHI104'),(N'MAX310',N'MAX309'),(N'VRP505',N'MAX310'),(N'HCM101',N'VRP505');
-- Frame: retain all requested subjects; assign at least 112 required + 28 elective credits.
-- Explicitly marked supplemental test subjects fill under-specified frames, not an accredited curriculum.
DECLARE @m int=1,@major nvarchar(20),@faculty nvarchar(20),@prefix nvarchar(3),@suffix nvarchar(2),@code nvarchar(20),@tc int,@sem int,@required int,@elective int,@n int,@kind nvarchar(50);
WHILE @m<=12 BEGIN
 SELECT @major=Code,@faculty=Faculty FROM #Majors WHERE Id=@m;
 SET @required=0;SET @elective=0;
 DECLARE subjects CURSOR LOCAL FAST_FORWARD FOR SELECT Code,TC,Sem FROM #Courses WHERE Scope='ALL' OR (@faculty='CNTT' AND Scope='IT') OR (@faculty='SP' AND (Scope='SP' OR Scope=@major)) ORDER BY Sem,Code;
 OPEN subjects;FETCH NEXT FROM subjects INTO @code,@tc,@sem;
 WHILE @@FETCH_STATUS=0 BEGIN
 SET @kind=CASE WHEN @required>=112 THEN N'Tự chọn' ELSE N'Bắt buộc' END;
 INSERT KhungChuongTrinh(MaNganh,MaMon,HocKyDuKien,LoaiMon) VALUES(@major,@code,@sem,@kind);
 IF @kind=N'Bắt buộc' SET @required+=@tc;ELSE SET @elective+=@tc;
 FETCH NEXT FROM subjects INTO @code,@tc,@sem;END;CLOSE subjects;DEALLOCATE subjects;
 SET @n=1;
 WHILE @required<112 OR @elective<28 BEGIN
 SET @kind=CASE WHEN @required<112 THEN N'Bắt buộc' ELSE N'Tự chọn' END;
 SET @tc=CASE WHEN @kind=N'Bắt buộc' THEN IIF(112-@required>=3,3,112-@required) ELSE IIF(28-@elective>=3,3,28-@elective) END;
 SET @code=CONCAT('BS_',@major,'_',@n);
 INSERT MonHoc(MaMon,TenMon,SoTinChi,SoTietLyThuyet,SoTietThucHanh,MaKhoa) VALUES(@code,CONCAT(N'Chuyên đề bổ sung kiểm thử ',@major,N' ',@n),@tc,@tc*15,0,@faculty);
 INSERT KhungChuongTrinh(MaNganh,MaMon,HocKyDuKien,LoaiMon) VALUES(@major,@code,1+(@n-1)%8,@kind);
 IF @kind=N'Bắt buộc' SET @required+=@tc;ELSE SET @elective+=@tc;
 SET @n+=1;END;SET @m+=1;END;
INSERT UserAccounts(Username,PasswordHash,RoleID,IsActive,NgayTao) SELECT 'admin','$2a$11$YukWnUFGbo0mxpuJ1oRt.uXLCWirdL52rAt5KnxbqtbkksicFfoD6',RoleID,1,SYSUTCDATETIME() FROM Roles WHERE RoleName='Admin';
INSERT UserAccounts(Username,PasswordHash,RoleID,IsActive,NgayTao) SELECT 'giaovu','$2a$11$M.9bHSEBSGR4TY6/SPVexOBB3eviDPy790Qg/Z8sZGKr9T6xxbRSa',RoleID,1,SYSUTCDATETIME() FROM Roles WHERE RoleName='GiaoVuKhoa';
DECLARE @uid int,@gv nvarchar(20),@role int=(SELECT RoleID FROM Roles WHERE RoleName='GiangVien');
SET @n=1;WHILE @n<=12 BEGIN
 SELECT @faculty=MaKhoa FROM (SELECT MaKhoa,ROW_NUMBER() OVER(ORDER BY MaKhoa) rn FROM Khoa)t WHERE rn=1+(@n-1)/3;
 SET @gv=CONCAT('GV',RIGHT('000'+CAST(@n AS varchar),3));
 INSERT UserAccounts(Username,PasswordHash,RoleID,IsActive,NgayTao,MaGV) VALUES(@gv,'$2a$11$M.9bHSEBSGR4TY6/SPVexOBB3eviDPy790Qg/Z8sZGKr9T6xxbRSa',@role,1,SYSUTCDATETIME(),@gv);SET @uid=SCOPE_IDENTITY();
 INSERT GiangVien(MaGV,HoTen,HocVi,MaKhoa,UserID,Email) VALUES(@gv,CONCAT(N'Giảng viên kiểm thử ',@n),N'Thạc sĩ',@faculty,@uid,CONCAT(@gv,'@agu.edu.vn'));SET @n+=1;END;
-- Eight chronological semester slots for each intake; same real term shared by intakes.
DECLARE @year int=2022,@h int,@hk nvarchar(20),@start date;
WHILE @year<=2027 BEGIN SET @h=1;WHILE @h<=2 BEGIN
 SET @hk=CONCAT('HK',@h,'_',RIGHT(CAST(@year AS varchar),2),RIGHT(CAST(@year+1 AS varchar),2));
 SET @start=CASE WHEN @h=1 THEN DATEFROMPARTS(@year,8,10) ELSE DATEFROMPARTS(@year+1,1,4) END;
 SET @start=DATEADD(day,-(DATEDIFF(day,CONVERT(date,'19000101'),@start)%7),@start);
 INSERT HocKy(MaHK,TenHK,NamHocBatDau,NamHocKetThuc,NgayBatDauTuan1,SoTuan,DonGiaTinChi,BatDauDangKy,KetThucDangKy)
 VALUES(@hk,CONCAT(N'Học kỳ ',@h),@year,@year+1,@start,20,IIF(@hk='HK2_2627',650000,635000),DATEADD(day,-10,@start),DATEADD(day,10,@start));
 SET @h+=1;END;SET @year+=1;END;
UPDATE HocKy SET NgayBatDauTuan1='20250811' WHERE MaHK='HK1_2526';UPDATE HocKy SET NgayBatDauTuan1='20251222' WHERE MaHK='HK2_2526';
UPDATE HocKy SET NgayBatDauTuan1='20260810',BatDauDangKy='20260801',KetThucDangKy='20261130' WHERE MaHK='HK1_2627';
CREATE TABLE #RoomNames(Id int PRIMARY KEY,Code nvarchar(50));INSERT #RoomNames VALUES(1,'NA301'),(2,'NA302'),(3,'NB109'),(4,'NB202'),(5,'NC101'),(6,'NC201'),(7,'ND201'),(8,'ND406'),(9,'ND503'),(10,'NE101'),(11,'NE201'),(12,'NMT06'),(13,'NMT09'),(14,'NHT150A'),(15,'NA303'),(16,'NB303'),(17,'NC303'),(18,'ND999');
INSERT PhongHoc(MaPhong,ToaNha,SucChua,TrangThai) SELECT Code,CASE SUBSTRING(Code,2,1) WHEN 'A' THEN 'A' WHEN 'B' THEN 'B' WHEN 'C' THEN 'C' WHEN 'D' THEN 'D' WHEN 'E' THEN 'E' ELSE 'Khac' END,60,IIF(Id=18,'BaoTri','HoatDong') FROM #RoomNames;
CREATE TABLE #Surname(Id int PRIMARY KEY,Val nvarchar(30));INSERT #Surname VALUES(0,N'Nguyễn'),(1,N'Trần'),(2,N'Lê'),(3,N'Phạm'),(4,N'Huỳnh'),(5,N'Võ'),(6,N'Phan'),(7,N'Đặng'),(8,N'Bùi'),(9,N'Đỗ'),(10,N'Hồ'),(11,N'Ngô'),(12,N'Dương'),(13,N'Lý'),(14,N'Đinh');
CREATE TABLE #Middle(Id int PRIMARY KEY,Val nvarchar(30));INSERT #Middle VALUES(0,N'Minh'),(1,N'Ngọc'),(2,N'Hoàng'),(3,N'Thanh'),(4,N'Gia'),(5,N'Bảo');
CREATE TABLE #Given(Id int PRIMARY KEY,Val nvarchar(30));INSERT #Given VALUES(0,N'An'),(1,N'Bình'),(2,N'Châu'),(3,N'Duy'),(4,N'Giang');
DECLARE @cohort int=22,@i int,@seq int=0,@sv nvarchar(20),@class nvarchar(20),@fullname nvarchar(255),@level int,@score float,@fee decimal(18,2),@section nvarchar(20),@historicalHK nvarchar(20),@sum int,@retake bit;
SET @role=(SELECT RoleID FROM Roles WHERE RoleName='SinhVien');
WHILE @cohort<=26 BEGIN SET @m=1;WHILE @m<=6 BEGIN
 SELECT @major=Code,@faculty=Faculty,@prefix=Prefix,@suffix=ClassSuffix FROM #Majors WHERE Id=@m;
 SELECT TOP 1 @gv=MaGV FROM GiangVien WHERE MaKhoa=@faculty ORDER BY MaGV;
 SET @class=CONCAT('DH',@cohort,@suffix);
 INSERT LopHanhChinh(MaLop,TenLop,NienKhoa,MaNganh,MaGVCN) VALUES(@class,CONCAT(N'Đại học ',@major,N' khóa ',@cohort),CONCAT(2000+@cohort,'-',2004+@cohort),@major,@gv);
 SET @i=1;WHILE @i<=15 BEGIN
 SET @sv=CONCAT(@prefix,@cohort,5800+@i);
 SELECT @fullname=CONCAT(s.Val,N' ',m.Val,N' ',g.Val) FROM #Surname s CROSS JOIN #Middle m CROSS JOIN #Given g WHERE s.Id=@seq%15 AND m.Id=(@seq/15)%6 AND g.Id=@seq/90;
 INSERT UserAccounts(Username,PasswordHash,RoleID,IsActive,NgayTao) VALUES(@sv,'$2a$11$M.9bHSEBSGR4TY6/SPVexOBB3eviDPy790Qg/Z8sZGKr9T6xxbRSa',@role,1,SYSUTCDATETIME());SET @uid=SCOPE_IDENTITY();
 INSERT SinhVien(MaSV,HoTen,NgaySinh,GioiTinh,CCCD,SDT,Email,DiaChi,MaLop,TrangThai,UserID) VALUES(@sv,@fullname,DATEFROMPARTS(1982+@cohort,1+@seq%12,1+@seq%28),IIF(@seq%2=0,N'Nam',N'Nữ'),CONCAT('089',RIGHT('000000000'+CAST(@seq+1 AS varchar),9)),CONCAT('09',RIGHT('00000000'+CAST(@seq+1 AS varchar),8)),CONCAT(@sv,'@student.agu.edu.vn'),CASE @seq%4 WHEN 0 THEN N'An Giang' WHEN 1 THEN N'Đồng Tháp' WHEN 2 THEN N'Cần Thơ' ELSE N'Kiên Giang' END,@class,N'Đang học',@uid);
 SET @level=CASE WHEN @cohort<=23 THEN 8 ELSE (26-@cohort)*2 END;SET @sum=0;
 DECLARE frame CURSOR LOCAL FAST_FORWARD FOR SELECT k.MaMon,m.SoTinChi,k.HocKyDuKien FROM KhungChuongTrinh k JOIN MonHoc m ON m.MaMon=k.MaMon WHERE k.MaNganh=@major ORDER BY k.HocKyDuKien,k.MaMon;
 OPEN frame;FETCH NEXT FROM frame INTO @code,@tc,@sem;
 WHILE @@FETCH_STATUS=0 BEGIN
 IF @sem<=@level AND NOT(@cohort<=23 AND @i IN(11,12) AND @sum+@tc>110) BEGIN
 SET @year=2000+@cohort+(@sem-1)/2;SET @h=1+(@sem-1)%2;
 -- Accelerated completion for the designated final-year test group, without future-dated grades.
 IF @cohort=23 AND @sem>=7 BEGIN SET @year=2025;SET @h=2;END;
 SET @historicalHK=CONCAT('HK',@h,'_',RIGHT(CAST(@year AS varchar),2),RIGHT(CAST(@year+1 AS varchar),2));
 SET @section=CONCAT('S',@cohort,'_',@code);
 IF NOT EXISTS(SELECT 1 FROM LopHocPhan WHERE MaLHP=@section) INSERT LopHocPhan(MaLHP,MaMon,MaHK,MaGV,TenLopHP,SiSoToiDa,NMH,STC,STCHP,MaLop,TrangThai,TrangThaiNhapDiem,TyLeQuaTrinh,TyLeThiKetThuc,CauHinhDiem) VALUES(@section,@code,@historicalHK,@gv,@code,500,'01',@tc,@tc,@class,N'Đã chốt',N'Đã khóa',40,60,N'[{"id":"col_cc","name":"Chuyên cần","weight":25},{"id":"col_gk","name":"Giữa kỳ","weight":75}]');
 SET @score=CASE WHEN @cohort<=23 AND @i=14 THEN 4.5 WHEN @cohort<=23 AND @i IN(13,15) AND @sum=0 THEN 3.0 WHEN @i<=3 THEN 9.2 WHEN @i<=6 THEN 8.2 ELSE 7.0 END;
 SET @retake=IIF(@i=10 AND @sum=0,1,0);IF @retake=1 SET @score=6.0;
 SET @fee=@tc*635000;
 INSERT KetQuaHocTap(MaSV,MaLHP,HocPhiDangKy,MienGiam,NgayDangKy,DiemCC,DiemGK,DiemCK,DiemThiLan1,DiemThiLan2,DiemTongKet,DiemChu,ChiTietDiemQT,GhiChu)
 VALUES(@sv,@section,@fee,IIF(@faculty='SP',@fee,0),DATEADD(day,-5,(SELECT NgayBatDauTuan1 FROM HocKy WHERE MaHK=@historicalHK)),@score,@score,@score,IIF(@retake=1,0,@score),IIF(@retake=1,9,NULL),@score,CASE WHEN @score>=9 THEN 'A+' WHEN @score>=8.5 THEN 'A' WHEN @score>=8 THEN 'B+' WHEN @score>=7 THEN 'B' WHEN @score>=5.5 THEN 'C' WHEN @score>=4 THEN 'D' ELSE 'F' END,CONCAT('{"col_cc":',@score,',"col_gk":',@score,'}'),IIF(@retake=1,N'Đạt (Thi lại, khống chế 6.0)',IIF(@score>=4,N'Đạt',N'Học lại')));
 SET @sum+=@tc;END;
 FETCH NEXT FROM frame INTO @code,@tc,@sem;END;CLOSE frame;DEALLOCATE frame;
 SET @seq+=1;SET @i+=1;END;
 SET @m+=1;END;SET @cohort+=1;END;

-- Current registrations: one teaching section per administrative class, actual tuition snapshot.
SET @m=1;SET @n=0;
WHILE @m<=6 BEGIN SELECT @major=Code,@faculty=Faculty FROM #Majors WHERE Id=@m;SELECT @gv=MaGV FROM (SELECT MaGV,ROW_NUMBER() OVER(ORDER BY MaGV)rn FROM GiangVien WHERE MaKhoa=@faculty)t WHERE rn=1+(@m-1)%3;
SET @cohort=22;WHILE @cohort<=26 BEGIN
SELECT @suffix=ClassSuffix FROM #Majors WHERE Id=@m;SET @class=CONCAT('DH',@cohort,@suffix);
SELECT TOP 1 @code=k.MaMon,@tc=m.SoTinChi FROM KhungChuongTrinh k JOIN MonHoc m ON m.MaMon=k.MaMon WHERE k.MaNganh=@major AND k.MaMon<>'COS101' ORDER BY ABS(k.HocKyDuKien-CASE WHEN @cohort<=23 THEN 7 ELSE (26-@cohort)*2+1 END),k.MaMon;
SET @section=CONCAT('NOW_',@class);
INSERT LopHocPhan(MaLHP,MaMon,MaHK,MaGV,TenLopHP,PhongHoc,SiSoToiDa,NMH,TTH,STC,STCHP,MaLop,TrangThai,TrangThaiNhapDiem,TyLeQuaTrinh,TyLeThiKetThuc,CauHinhDiem)
VALUES(@section,@code,'HK1_2627',@gv,CONCAT(N'Lớp ',@code),(SELECT Code FROM #RoomNames WHERE Id=1+@n%17),40,'01','1',@tc,@tc,@class,'MoDangKy',N'Chưa nhập',CASE @n%3 WHEN 0 THEN 40 WHEN 1 THEN 50 ELSE 100 END,CASE @n%3 WHEN 0 THEN 60 WHEN 1 THEN 50 ELSE 0 END,N'[{"id":"col_cc","name":"Chuyên cần","weight":25},{"id":"col_gk","name":"Giữa kỳ","weight":75}]');
-- Each instructor has a separate day/period slot; two sessions per class, different weeks for alternating delivery.
INSERT ClassSchedule(MaLHP,LoaiLich,LoaiBuoiHoc,Thu,TietBatDau,SoTiet,PhongHoc,MaGV,TuanBaoNghi,ChuoiTuanHoc)
VALUES(@section,'LichHoc',0,2+@cohort-22,IIF(@n%2=0,1,6),3,(SELECT Code FROM #RoomNames WHERE Id=1+@n%17),@gv,IIF(@n%7=0,'12',''),'1-15');
INSERT ClassSchedule(MaLHP,LoaiLich,LoaiBuoiHoc,Thu,TietBatDau,SoTiet,PhongHoc,MaGV,TuanBaoNghi,ChuoiTuanHoc)
VALUES(@section,'LichHoc',1,2+@cohort-22,IIF(@n%2=0,4,9),2,(SELECT Code FROM #RoomNames WHERE Id=1+@n%17),@gv,'','1-15');
SET @fee=@tc*(SELECT DonGiaTinChi FROM HocKy WHERE MaHK='HK1_2627');
INSERT KetQuaHocTap(MaSV,MaLHP,HocPhiDangKy,MienGiam,NgayDangKy) SELECT MaSV,@section,@fee,IIF(@faculty='SP',@fee,0),'20260801' FROM SinhVien WHERE MaLop=@class;
SET @n+=1;SET @cohort+=1;END;SET @m+=1;END;
INSERT LopHocPhan(MaLHP,MaMon,MaHK,MaGV,TenLopHP,SiSoToiDa,NMH,STC,STCHP,TrangThai,TyLeQuaTrinh,TyLeThiKetThuc) VALUES('SEED_FULL','COS101','HK1_2627','GV001',N'Lớp đủ 30/30',30,'02',3,3,'MoDangKy',40,60);
INSERT KetQuaHocTap(MaSV,MaLHP,HocPhiDangKy,MienGiam,NgayDangKy) SELECT MaSV,'SEED_FULL',1905000,0,'20260801' FROM SinhVien WHERE MaLop IN('DH24TH','DH25TH');
-- Portal curricula and 11 standard sections, generated from actual test frames.
CREATE TABLE #Titles(n int PRIMARY KEY,title nvarchar(250));
INSERT #Titles VALUES(1,N'GIỚI THIỆU CHUNG'),(2,N'ĐỐI TƯỢNG TUYỂN SINH'),(3,N'QUY CHẾ ĐÀO TẠO'),(4,N'CHUẨN ĐẦU RA'),(5,N'THANG PHÂN LOẠI NHẬN THỨC, KỸ NĂNG, THÁI ĐỘ'),(6,N'CHƯƠNG TRÌNH ĐÀO TẠO'),(7,N'CÁC MÔN HỌC VÀ MỐI QUAN HỆ VỚI CHUẨN ĐẦU RA'),(8,N'CÁC CHUỖI MÔN HỌC'),(9,N'KẾ HOẠCH GIẢNG DẠY'),(10,N'ĐIỀU KIỆN TỐT NGHIỆP'),(11,N'MÔ TẢ VẮN TẮT NỘI DUNG VÀ KHỐI LƯỢNG CÁC MÔN HỌC');
DECLARE @cid int,@html nvarchar(max),@title nvarchar(250);
SET @m=1;WHILE @m<=12 BEGIN SELECT @major=Code FROM #Majors WHERE Id=@m;SET @year=2022;WHILE @year<=2026 BEGIN
INSERT Curriculums(HeDaoTao,KhoaHoc,NganhHocId,TenChuongTrinh,TrangThai,NgayTao,NgayCapNhat) VALUES(N'Đại học chính quy',@year,@major,N'Chính quy',1,SYSUTCDATETIME(),SYSUTCDATETIME());SET @cid=SCOPE_IDENTITY();
SET @n=1;WHILE @n<=11 BEGIN SELECT @title=title FROM #Titles WHERE n=@n;
SET @html=CONCAT(N'<p>Dữ liệu mô phỏng kiểm thử cho ngành ',@major,N', khóa ',@year,N'.</p>');
IF @n IN(6,9,11) BEGIN
SET @html+=N'<table><thead><tr><th>Mã môn</th><th>Tên môn</th><th>Tín chỉ</th><th>Học kỳ</th></tr></thead><tbody>';
SELECT @html+=CONCAT(N'<tr><td>',m.MaMon,N'</td><td>',m.TenMon,N'</td><td>',m.SoTinChi,N'</td><td>',k.HocKyDuKien,N'</td></tr>') FROM KhungChuongTrinh k JOIN MonHoc m ON m.MaMon=k.MaMon WHERE k.MaNganh=@major;SET @html+=N'</tbody></table>';END;
IF @n=10 SET @html+=N'<p>Tối thiểu 140 tín chỉ; 112 bắt buộc; 28 tự chọn; GPA hệ 4 từ 2.0; không còn môn F.</p>';
INSERT CurriculumSections(CurriculumId,ThuTu,TieuDeMuc,NoiDung) VALUES(@cid,@n,@title,@html);SET @n+=1;END;SET @year+=1;END;SET @m+=1;END;
INSERT ChucNang(MaChucNang,TenChucNang) VALUES('TONG_QUAN',N'Tổng quan'),('THONG_KE',N'Thống kê'),('SINH_VIEN',N'Sinh viên'),('GIANG_VIEN',N'Giảng viên'),('MON_HOC',N'Môn học'),('KHUNG_DAO_TAO',N'Khung đào tạo'),('LOP_HOC_PHAN',N'Lớp học phần'),('LOP_HANH_CHINH',N'Lớp hành chính'),('DIEM_SO',N'Điểm số'),('TRA_CUU_DIEM',N'Tra cứu điểm'),('KHIEU_NAI',N'Phúc khảo'),('TAI_KHOAN',N'Tài khoản'),('NHAT_KY',N'Nhật ký');
INSERT HanhDong(MaHanhDong,TenHanhDong) VALUES('VIEW',N'Xem'),('CREATE',N'Thêm'),('EDIT',N'Sửa'),('DELETE',N'Xóa'),('IMPORT',N'Nhập'),('EXPORT',N'Xuất');
INSERT PhanQuyen SELECT r.RoleID,c.MaChucNang,h.MaHanhDong FROM Roles r CROSS JOIN ChucNang c CROSS JOIN HanhDong h WHERE r.RoleName='Admin' OR (r.RoleName='SinhVien' AND c.MaChucNang IN('TONG_QUAN','TRA_CUU_DIEM','KHIEU_NAI') AND h.MaHanhDong='VIEW') OR (r.RoleName='GiangVien' AND c.MaChucNang IN('TONG_QUAN','DIEM_SO','LOP_HOC_PHAN','KHIEU_NAI') AND h.MaHanhDong IN('VIEW','EDIT')) OR (r.RoleName='GiaoVuKhoa' AND c.MaChucNang IN('SINH_VIEN','DIEM_SO') AND h.MaHanhDong='VIEW');
INSERT PhanQuyenPhamVi(RoleID,MaPhamVi,Module,[Rule],IsActive) SELECT RoleID,'SELF','ALL','SELF',1 FROM Roles;
INSERT Sessions(UserID,RefreshToken,ExpiresAt,IsRevoked,CreatedAt,UpdatedAt) SELECT UserID,CONCAT('seed-expired-',UserID),'20200101',1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM UserAccounts WHERE Username IN('admin','GV001');
SET @uid=(SELECT UserID FROM UserAccounts WHERE Username='admin');SET @n=1;
WHILE @n<=10 BEGIN
INSERT Article(TieuDe,TomTat,NoiDung,DanhMuc,HeDaoTao,DonViDang,NguoiDangId,NgayDang,GhimDauTrang,TrangThai,XuatBanLuc) VALUES(CONCAT(N'Thông tin kiểm thử số ',@n),N'Dữ liệu mẫu phục vụ kiểm thử Portal',N'<p>Thông báo mô phỏng, không phải thông báo chính thức.</p>',CASE @n%7 WHEN 0 THEN 'ThongBaoChung' WHEN 1 THEN 'NghiBu' WHEN 2 THEN 'HocPhi' WHEN 3 THEN 'HuongDan' WHEN 4 THEN 'TuyenSinh' WHEN 5 THEN 'HocBong' ELSE 'TinTuc' END,N'Đại học chính quy',N'Phòng Đào tạo',@uid,DATEADD(day,-@n,'20261104'),IIF(@n=1,1,0),'DaXuatBan',DATEADD(day,-@n,'20261104'));SET @n+=1;END;
SET @n=1;WHILE @n<=8 BEGIN
INSERT PortalDocument(TieuDe,Loai,FileUrl,DungLuongBytes,HienThi) VALUES(CONCAT(N'Tài liệu mẫu kiểm thử ',@n),IIF(@n<=4,'QuyChe','BieuMau'),'/portal#documents',0,0);SET @n+=1;END;
-- Documents remain hidden until real downloadable files are supplied.
SET @n=1;WHILE @n<=5 BEGIN
SELECT @sv=MaSV FROM (SELECT MaSV,ROW_NUMBER() OVER(ORDER BY MaSV)rn FROM SinhVien)t WHERE rn=@n;
SELECT TOP 1 @section=MaLHP FROM KetQuaHocTap WHERE MaSV=@sv AND DiemTongKet IS NOT NULL ORDER BY MaKQ;
INSERT DonKhieuNai(MaSV,MaLHP,LyDo,LoaiDiem,NgayGui,TrangThai,PhanHoi,DaXem) VALUES(@sv,@section,N'Đề nghị kiểm tra điểm (mô phỏng).','DiemThiLan1','20261020',CASE @n%3 WHEN 0 THEN N'Đã duyệt' WHEN 1 THEN N'Chờ xử lý' ELSE N'Từ chối' END,NULL,0);
INSERT ThongBao(MaNguoiGui,MaNguoiNhan,TieuDe,NoiDung,LoaiThongBao,NgayGui,DaDoc) VALUES('admin',@sv,N'Thông báo kiểm thử',N'Đã tiếp nhận yêu cầu.','SEED','20261020',0);SET @n+=1;END;
INSERT NhatKyHoatDong(NguoiDung,ThoiGian,HanhDong,ChiTiet,Loai) VALUES('admin',SYSUTCDATETIME(),N'Seed dữ liệu',N'450 SV / 30 lớp / 5 khóa, dữ liệu giả lập',N'Thông tin');
IF (SELECT COUNT(*) FROM SinhVien)<>450 OR (SELECT COUNT(*) FROM LopHanhChinh)<>30 THROW 51001,N'Incorrect student/class count',1;
IF EXISTS(SELECT SUBSTRING(MaSV,4,2) FROM SinhVien GROUP BY SUBSTRING(MaSV,4,2) HAVING COUNT(*)<>90) THROW 51002,N'Each intake must have 90 students',1;
IF (SELECT COUNT(DISTINCT HoTen) FROM SinhVien)<>450 THROW 51003,N'Duplicate generated names',1;
IF EXISTS(SELECT 1 FROM KetQuaHocTap k JOIN SinhVien s ON s.MaSV=k.MaSV JOIN LopHanhChinh l ON l.MaLop=s.MaLop JOIN Nganh n ON n.MaNganh=l.MaNganh WHERE (n.MaKhoa='SP' AND k.MienGiam<>k.HocPhiDangKy) OR (n.MaKhoa='CNTT' AND k.MienGiam<>0)) THROW 51004,N'Tuition exemption mismatch',1;
COMMIT;
SELECT SUBSTRING(MaSV,4,2) Khoa,COUNT(*) SoSinhVien FROM SinhVien GROUP BY SUBSTRING(MaSV,4,2) ORDER BY Khoa;
SELECT COUNT(*) SoTaiKhoanSinhVien FROM UserAccounts u JOIN Roles r ON r.RoleID=u.RoleID WHERE r.RoleName='SinhVien';
SELECT COUNT(*) SoKetQua FROM KetQuaHocTap;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
