# Tối ưu truy vấn Academic Ledger

Đã áp dụng vào mã nguồn và bổ sung index trên **QuanLyDiemSV_Demo**, đúng database đang được appsettings.json sử dụng. Không chạy lại seed hoặc đổi connection string trong lần tối ưu này.

## Thay đổi

- `LopHocPhan.GetAll`: mặc định xác định học kỳ theo ngày Việt Nam; có thể truyền `maHK` để xem học kỳ khác. `AsNoTracking`, `Where`, thứ tự ổn định theo mã, `Skip/Take` ở SQL; chỉ chiếu các cột danh sách, bỏ HTML/cấu hình điểm và hồ sơ giảng viên dư thừa. Sĩ số và số đã nhập điểm chỉ thống kê theo mã lớp của trang đang xem.
- `LichHoc.Index`: lọc học kỳ/khoa/khóa/trạng thái/từ khóa trước phân trang. Chỉ tải lịch và thống kê của trang đó; không tải entity bảng điểm/sinh viên. Liên kết tới một mã lớp tự tìm trang chứa lớp đó. Giữ form chỉnh sửa lịch và cấu hình học kỳ/phòng học.
- `LichHoc.Matrix`: lọc học kỳ, ngày, loại lịch, phòng thuộc tòa nhà ngay trong SQL; chiếu thông tin card, không Include bảng điểm. Sĩ số truy vấn riêng cho các lớp thực sự hiển thị. Lưới giữ đủ buổi học để không hiển thị sai phòng trống.
- `SinhVien.GetAll`: phân trang/tìm kiếm/lọc lớp, ngành, khoa, niên khóa, trạng thái tại SQL; dữ liệu dòng chỉ chiếu hồ sơ và lớp hành chính. Chỉ khi lọc khoa/ngành mới cần quan hệ đó trong điều kiện; tên ngành/khoa trên UI dùng danh mục đã tải sẵn.
- `LopHanhChinh.GetAll`: chiếu danh sách và Count tại SQL, không Include cả collection sinh viên.
- Danh mục môn học/tiên quyết dùng `IMemoryCache` 15 phút, hai truy vấn phẳng khi cache trống, không SQL khi cache đã có. Thêm/sửa/xóa/import môn học vô hiệu hóa cache. Khung chương trình ghép kết quả theo ngành với snapshot danh mục thay cho chuỗi Include sâu. Kiểm tra ghi/đăng ký vẫn đọc dữ liệu hiện hành, không dùng cache kết quả học tập.
- Bài viết Portal đã có `TrangThai == DaXuatBan` trước phân trang; giữ bộ lọc đó và bổ sung khóa Id để thứ tự ổn định. Các danh sách public/Admin bỏ tải `NoiDung`; Admin tải nội dung theo mã khi mở preview/xuất PDF. Admin vẫn xem được bài nháp của phạm vi được phép.
- TKB dùng projection riêng; trang đăng ký học phần dùng truy vấn không tracking, tách collection, thống kê sĩ số riêng. Popup xếp sinh viên dùng EXISTS cho điều kiện học tập, không tải bảng điểm của toàn bộ sinh viên.

## Giao diện/API

Ví dụ:

```text
GET /api/lophocphan?maHK=HK1_2627&page=1&pageSize=20&search=NOW
GET /api/sinhvien?maLop=DH26TH&page=2&pageSize=10
GET /quan-ly-lich-hoc?maHK=HK1_2627&page=1&pageSize=20
```

API danh sách trả `data`, `total`, `page`, `pageSize`, `totalPages`; API lớp trả thêm `maHK`, `open`, `locked`. Giới hạn tối đa 100 dòng/trang API, 50 dòng/trang điều phối.

Đã cập nhật các trang quản lý, tra cứu điểm, sổ điểm, thống kê và dashboard dùng các API này. Dropdown lớp dùng `/api/lophocphan/options` (mã/tên/môn/học kỳ, giới hạn một học kỳ); KPI tổng lớp dùng `/api/lophocphan/count` thay cho tải mọi dòng. Xuất Excel sinh viên chủ động đọc lần lượt các trang đúng bộ lọc, không chỉ xuất trang hiện tại. Ô tìm kiếm có debounce và bỏ qua phản hồi cũ để tránh ghi đè kết quả mới.

## Index

`Optimize_QuanLyDiemSV_Indexes.sql` có USE QuanLyDiemSV_Demo ở đầu; đổi duy nhất dòng này nếu chủ đích tối ưu database gốc QuanLyDiemSV. Script có transaction, thời gian chờ khóa hữu hạn và kiểm tra index tương đương theo tiền tố khóa; có thể chạy lại.

Trên Demo: tạo index `(MaLHP,Thu)` cho ClassSchedule, `(MaSV,MaLHP)` và `(MaLHP,DiemTongKet)` cho KetQuaHocTap, `(TrangThai,GhimDauTrang,NgayDang,Id)` cho Article. Tái sử dụng index MaHK và MaNganh hiện có. ApplicationDbContext cũng khai báo các index; script schema đầy đủ được xuất lại để database mới có cùng cấu hình.

Không gọi EnsureCreated để nâng cấp database đang có: thay đổi mapping index không tự thêm index vào database cũ, cần script trên hoặc migration.

## Kiểm chứng

Dữ liệu: 450 SV, 484 LHP, 14.713 kết quả học tập. Bản build có Razor thành công. Bộ kiểm tra chỉ đọc `tools/PerformanceChecks` đối chiếu sĩ số/điểm thật, phân trang không chồng dòng, lọc học kỳ, cache/invalidation và kiểm tra câu SQL không còn JOIN bảng điểm ở ma trận.

Kết quả đo lần đầu sau tối ưu (backend trực tiếp):

| Kiểm tra | Thời gian |
|---|---:|
| API lớp lần đầu (gồm khởi tạo EF và đối chiếu) | 1.577 ms |
| API lớp khi đã khởi tạo | 30 ms |
| Sinh viên + kiểm tra tổng | 51 ms |
| Điều phối lần đầu | 175 ms |
| Điều phối lần tiếp theo | 60 ms |
| Ma trận | 63 ms |
| Danh mục lần thứ hai | 0 câu SQL |

Lần chạy xác nhận tiếp theo: API lớp warm 20 ms, sinh viên 32 ms, điều phối warm 59 ms, ma trận 71 ms; kiểm tra học kỳ cũ và liên kết mở lớp ngoài trang đầu cũng đạt. Script index chạy lại tái sử dụng đủ sáu index tương đương, không tạo trùng.

Đây là số đo tại máy này, không phải cam kết mọi yêu cầu dưới 0,5 giây. Chưa bao gồm trình duyệt, CDN, mạng; lần đầu khởi tạo EF vẫn trên 0,5 giây. Các lần kiểm tra trước từng có timeout kết nối SQL, nên không coi tối ưu LINQ là bằng chứng mọi vấn đề kết nối đã hết.

Chạy từ thư mục gốc:

```powershell
$checkOutput = Join-Path (Get-Location) '.verification'
dotnet build tools/PerformanceChecks/PerformanceChecks.csproj --no-restore -p:UseAppHost=false "-p:OutputPath=$checkOutput/"
dotnet (Join-Path $checkOutput 'PerformanceChecks.dll')
node tools/PerformanceChecks/frontend-checks.cjs
```

Công cụ đo đọc kết nối từ appsettings.json, không chạy initializer, không sửa dữ liệu. Bộ kiểm tra frontend xác nhận chuyển trang/bộ lọc thực sự gửi query params và xuất Excel qua đủ trang.

Để nhận thay đổi constructor/Controller/ViewModel/Razor: dừng dotnet watch bằng Ctrl+C rồi chạy lại dotnet watch run trong QL_DiemSV. Không cần chạy lại seed.
