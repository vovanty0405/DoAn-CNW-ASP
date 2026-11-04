# SeedData_QuanLyDiemSV.sql

Chạy Database_QLDiemSV_Full.sql trước, sau đó chạy SeedData_QuanLyDiemSV.sql bằng SSMS. Chỉ dùng trên database có schema và mọi bảng còn trống. Script từ chối dữ liệu sẵn có; không TRUNCATE/DELETE, không vô hiệu khóa ngoại. Toàn bộ seed ở trong transaction, lỗi sẽ rollback. Không chạy lại trên database đã seed.

## Sinh viên và tài khoản

30 lớp, 450 sinh viên và đúng 450 tài khoản vai trò SinhVien; ngoài ra có admin, giaovu và 12 giảng viên. Mật khẩu admin là admin123; các tài khoản còn lại là 123456. Cột PasswordHash lưu BCrypt, chỉ dùng tài khoản này cho kiểm thử.

Mỗi khóa 22–26 có 90 SV, mỗi ngành 15 SV: CNTT/DTH/TH, KTPM/DPM/PM, KHMT/DKH/KH, SPTOAN/DTO/TO, SPAV/DTA/TA, SPVL/DLY/LY. Mã theo ví dụ 5801–5815, không dùng 5800. Lớp ví dụ DH23TH; niên khóa 2023-2027. Tên được ghép từ bảng tạm 15 họ × 6 đệm × 5 tên, tạo 450 tổ hợp khác nhau không thêm số vào họ tên. Ngày sinh năm 2004–2008 tương ứng khóa 22–26, CCCD 12 số, điện thoại 10 số, email MaSV@student.agu.edu.vn; toàn bộ đều là dữ liệu giả lập.

Dùng WHILE theo khóa → ngành → sinh viên; lịch sử dùng cursor khung đào tạo bên trong. IDENTITY tự sinh và lấy SCOPE_IDENTITY, không chèn ID cố định nên không cần IDENTITY_INSERT.

## Điểm và tốt nghiệp

Khóa 22 và nhóm mô phỏng hoàn thành sớm khóa 23: số thứ tự 5801–5810 mỗi lớp đạt đủ >=112 tín chỉ bắt buộc và >=28 tự chọn, GPA >=2; có nhóm GPA 4.0 và 3.5. Nhóm 5811–5812 chỉ học tối đa 110 TC; 5813/5815 có F; 5814 đạt môn nhưng GPA 1.0. Tổng 120 trường hợp đạt và 60 không đạt cho hai khóa cuối. Mười SV đầu khóa 24–26 chưa bị coi là tốt nghiệp; chỉ seed phần đã học theo năm học hiện tại.

Khóa 23 hoàn thành sớm là dữ liệu kiểm thử, gộp các môn HK7/8 về HK2_2526 để tránh điểm tương lai. Khóa 24/25/26 có lịch sử 4/2/0 học kỳ, đăng ký hiện tại HK1_2627. Khóa 22 cũng có đăng ký mô phỏng hiện tại nhưng không có điểm, không ảnh hưởng xét tín chỉ đã đạt.

Sinh viên có STT 5810 có một môn thi lại: QT=6, thi lần 1=0, thi lần 2=9; tổng kết tối đa 6.0 theo GradeHelper. JSON điểm quá trình dùng col_cc/col_gk tương thích cấu hình cột. Mọi học phí đều là STCHP × đơn giá; Sư phạm được MienGiam bằng toàn bộ HocPhiDangKy, CNTT không miễn giảm. Lớp SEED_FULL chứa đúng 30 SV, sĩ số tối đa 30.

## Phạm vi dữ liệu

Có dữ liệu cho 28 bảng, 4 khoa, 12 ngành, 12 GV; các CTĐT Portal có 11 mục, các môn và tiên quyết trích từ danh sách yêu cầu. Do danh sách môn SP và một số ngành chưa đạt 140 tín chỉ, script thêm các môn mang mã BS_ và tên Chuyên đề bổ sung kiểm thử; đây không phải chương trình đào tạo được công nhận. Khung CNTT có thể vượt 140 do giữ tất cả môn đã yêu cầu; tiêu chí là >=140, không ép sửa tín chỉ thực của các môn.

Tạo thêm học kỳ lịch sử từ 2022 để đặt điểm đúng năm; giữ bốn học kỳ đã yêu cầu, đơn giá HK2_2627=650000. Các giờ/ngày trong seed là dữ liệu mô phỏng cố định theo HK1_2627, không tự dời theo ngày chạy. Có LT/TH và tuần báo nghỉ 12. Có 18 phòng, một phòng bảo trì.

8 bản ghi PortalDocument là metadata đang ẩn, dung lượng 0; không giả lập file PDF tồn tại. Cần thêm file thật rồi cập nhật FileUrl/DungLuongBytes/HienThi để kiểm thử tải file. Phiên Sessions mẫu đã hết hạn và bị thu hồi. Article, khiếu nại, thông báo và log đều ghi rõ mô phỏng.

Trình kiểm chứng: tools/SeedDataChecks chạy seed trên CSDL AcademicLedger_SeedCheck_GUID, so sánh tốt nghiệp bằng GradeHelper và tự xóa CSDL tạm trong finally. Không chạy seed vào database hiện tại của người dùng.
