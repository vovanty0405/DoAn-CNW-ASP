# Academic Ledger — Đăng ký học phần và Thời khóa biểu

## Sử dụng

- Sinh viên: `/dang-ky-hoc-phan`, `/thoi-khoa-bieu`.
- Giảng viên: `/thoi-khoa-bieu` chỉ hiển thị các buổi được phân công theo MaGV.
- Admin: `/quan-ly-lich-hoc`, `/quan-ly-lich-hoc/ma-tran`; có liên kết trong quản lý lớp học phần và menu bên trái.

Tạo học kỳ/lớp trong màn hình quản lý lớp hiện có. Trong Điều phối lịch, chọn lớp, khai báo phòng và sức chứa, cấu hình ngày thứ Hai đầu tuần 1, số tuần, thời gian mở/đóng đăng ký (giờ Việt Nam) và đơn giá tín chỉ. Học kỳ chưa cấu hình thời gian đăng ký sẽ đóng đăng ký. Khai báo NMH/TTH/STC/STCHP/lớp hành chính và thêm các buổi LT/TH, sau đó lưu.

Tuần học lưu dưới dạng danh sách số, hỗ trợ khoảng khi nhập: `1-5,8,10-12`. Không dùng chuỗi rút gọn mơ hồ như `-----6-89012`. Chủ nhật lưu là 8, thứ Hai là 2. Tiết học trong 1–12, khoảng kết thúc không vượt tiết 12.

Sinh viên chọn lớp, kiểm tra học phí rồi lưu đăng ký. Xóa khỏi bảng dưới chỉ thay đổi lựa chọn; bấm Lưu Đăng Ký để hủy trong CSDL. Các môn đã có điểm không được hủy. Kiểm tra tiên quyết yêu cầu điểm tổng kết >= 4. Kiểm tra trùng nhóm/lịch ở cả trình duyệt và máy chủ; sĩ số và đợt đăng ký được kiểm tra khi lưu trong transaction Serializable.

Học phí = STCHP × đơn giá, riêng STC = 0 thì học phí = 0. Học phí được chụp lại vào `KetQuaHocTap.HocPhiDangKy` khi lưu; không nhận học phí/miễn giảm từ trình duyệt. `MienGiam` là số tiền được phê duyệt trên hồ sơ đăng ký, mặc định 0. Phải đóng = học phí − miễn giảm. Chưa bổ sung quy trình xét duyệt miễn giảm.

TKB tuần dùng rowspan và bỏ TD ở các tiết đã bị gộp. TKB toàn học kỳ là bảng liệt kê buổi học và tuần tương ứng. In TKB / Lưu PDF mở hộp thoại in trình duyệt, chọn Save as PDF để xuất; có CSS A4 ngang. Admin xem ma trận phòng thay vì gộp các lớp khác phòng vào một lưới.

## CSDL

Ứng dụng vẫn dùng SQL Server và cơ chế EnsureCreated hiện có. `AcademicSchemaUpgrade.Apply` nâng cấp cộng thêm, có transaction và khóa ứng dụng; chạy trước điều kiện bỏ qua seed, nên áp dụng cho cả CSDL đã có dữ liệu. Không xóa bảng cũ. Tín chỉ của lớp hiện có được điền từ MonHoc trong lần bổ sung cột. Kết quả đăng ký dùng lại KetQuaHocTap, tương thích các màn hình điểm và xếp lớp cũ. SoLuongDaDangKy được tính từ số bản ghi đăng ký để tránh bộ đếm lệch.

Tài khoản kết nối SQL cần quyền ALTER/CREATE để nâng cấp schema. Không cần chạy `dotnet ef database update` cho thay đổi này; dự án hiện chưa dùng EF migrations.

## Kiểm tra

```
dotnet build QL_DiemSV/QL_DiemSV.csproj --no-restore
dotnet run --project tests/AcademicLedger.Checks/AcademicLedger.Checks.csproj
dotnet run --project tests/AcademicLedger.Checks/AcademicLedger.Checks.csproj -- --database
node --check QL_DiemSV/wwwroot/js/registration.js
```

Kiểm thử `--database` dùng SQLEXPRESS01, tạo database tên `AcademicLedger_Check_<guid>` riêng, kiểm tra lưu lịch, xung đột phòng/GV, cảnh báo sức chứa, học phí, đăng ký và hủy đăng ký; luôn xóa database tạm trong finally. Không ghi dữ liệu thử vào QuanLyDiemSV.

`tests/AcademicLedger.Checks/http_smoke.py` kiểm tra các trang với tài khoản seed khi ứng dụng chạy ở localhost:5278. Chỉ dùng trên môi trường phát triển có tài khoản mẫu; đăng nhập sẽ ghi session/nhật ký như thông thường.

## Nâng cấp Điều phối lịch & Phòng học

- Mã lớp hành chính là dropdown lấy từ LopHanhChinh, hiển thị `MaLop - TenLop` và giữ đúng mã đang lưu.
- Khung Cấu hình Học kỳ có dropdown riêng (`configHK`), độc lập với học kỳ của lớp học phần. Sau khi lưu vẫn giữ lớp và học kỳ đang cấu hình.
- Phòng học bổ sung ToaNha (A–E, Sân tập, Phòng họp, Khác) và TrangThai (Hoạt động, Bảo trì, Đang khóa). Phòng cũ mặc định khu vực Khác / Hoạt động; sửa phòng để gán khu vực thực tế.
- Bảng phòng hỗ trợ sửa/đổi mã và xóa. Đổi mã chuyển các tham chiếu lịch và trường phòng cũ của lớp trong một transaction; xóa bị chặn nếu phòng còn lịch hoặc lớp tham chiếu. Phòng bảo trì/khóa giữ lịch cũ nhưng chặn thao tác xếp lịch vào phòng đó.
- Buổi học bổ sung LoaiLich: Lịch học, Lịch thi, Dạy bù, Họp, Khác. Chọn loại trong bảng sửa buổi; các lịch cũ được phân loại Lịch học. Các loại này vẫn liên kết với lớp học phần, không phải hệ thống đặt phòng sự kiện độc lập.
- Ma trận có lọc loại lịch/khu vực; Theo tuần hiển thị một bảng Phòng (rowspan 12) × Tiết × 7 ngày; Theo ngày hiển thị ngày chọn; Rút gọn chỉ hiển thị phòng có lịch và khoảng tiết đang dùng của ngày đó. Các bộ lọc giữ nguyên khi chuyển chế độ hoặc điều hướng ngày.
- Datepicker quy đổi theo ngày bắt đầu tuần 1. Ngày ngoài học kỳ hiển thị cảnh báo và không tự chuyển sang một tuần khác. Khi đổi học kỳ/tuần/thứ, ngày được tính lại. Hôm qua/Ngày mai di chuyển một ngày từ ngày đang xem; Hôm nay dùng giờ Việt Nam.
- Mỗi tiết 50 phút, có các khoảng nghỉ theo yêu cầu. Tiết 11–12 chưa được quy định trong yêu cầu nên dùng 17:30–18:20 và 18:20–19:10; chỉnh tại RoomMatrixRules khi trường có khung giờ chính thức.
- Các buổi liên tục dùng colspan và bỏ ô tiết đã gộp. Dữ liệu cũ có xung đột được gom vào khối cảnh báo, giữ đầy đủ các lịch và không làm lệch 12 cột.

## Ma trận hiện tại, điều hướng tuần và bố cục vừa màn hình

Mặc định `Matrix` nhận week/day nullable để phân biệt truy cập đầu với lựa chọn tường minh. `RoomMatrixRules.ResolveCalendar` lấy ngày Việt Nam, chọn học kỳ có NgayBatDauTuan1 <= ngày < NgayBatDauTuan1 + SoTuan × 7. Nếu nhiều học kỳ giao nhau, chọn học kỳ bắt đầu gần nhất; nếu không có học kỳ hiện tại, chọn học kỳ gần ngày tra cứu nhất kèm cảnh báo, vẫn giữ đúng ngày thật và không giả lập tuần hiện tại. Không suy đoán học kỳ chỉ từ năm học.

Ngày tường minh có ưu tiên hơn week/day có thể còn cũ trong URL. Khi chỉ gửi week/day, ngày được dựng lại từ tuần 1. Đổi tab reload URL viewMode nên bảng, tab active và bộ lọc luôn được render chung một trạng thái. Các bộ lọc loại lịch/khu vực được giữ lại.

- Theo ngày: đủ 12 tiết, chọn ngày/thứ và nút Hôm nay (T6, 30/10/2026); nút Hôm nay tự tìm lại học kỳ chứa hôm nay thay vì giữ học kỳ cũ.
- Theo tuần: một bảng có cột Phòng gộp 12 dòng, cột Tiết kèm giờ và bảy cột ngày kèm dd/MM. Chỉ hiện điều hướng tuần; buổi học gộp dọc bằng rowspan, bỏ TD ở tiết đã gộp. Tuần hiện tại cũng tự tìm học kỳ tương ứng.
- Rút gọn: bộ lọc theo ngày, ẩn phòng trống và các tiết trước/sau khoảng có lịch; vẫn giữ tiết trống ở giữa để không sai độ dài colspan.
- Bảng có width:100%, table-layout:fixed, cột theo phần trăm; bỏ min-width:1500px. Khung bảng overflow-x:hidden, thời gian/card được xuống dòng. Padding của main chỉ giảm cho trang ma trận, không thay đổi các trang khác.
- Thanh Chú thích dùng ô màu Đã đặt phòng, dấu — cho Trống, vàng cho Quá sức chứa và đỏ cho Xung đột.

Mã nguồn chính: Controllers/LichHocController.cs (Matrix), Services/RoomMatrixRules.cs (ResolveCalendar), ViewModels/AcademicViewModels.cs (RoomMatrixViewModel), Views/LichHoc/Matrix.cshtml và _MatrixBooking.cshtml, wwwroot/css/academic.css, wwwroot/js/room-matrix.js.

Nếu ứng dụng đang chạy khóa DLL, dừng phiên chạy rồi build lại. Có thể build kiểm tra sang một OutputPath riêng (.verification) mà không dừng ứng dụng hiện tại.

## Cấu hình học kỳ — form và bảng đồng bộ danh mục phòng

Khối `Views/LichHoc/_TermConfiguration.cshtml` dùng cùng panel, section-heading, badge, toolbar, scroll/table và row-actions với phòng học. Bảng liệt kê mọi phần tử `ScheduleEditorViewModel.Terms`, gồm tên/năm học, ngày tuần 1 và số tuần, khoảng đăng ký, đơn giá, trạng thái, Sửa/Xóa.

Dropdown Chọn học kỳ và nút Sửa dùng dữ liệu JSON được Razor mã hóa trong trang, điền đủ trường ngay lập tức, không reload. Hủy chọn bỏ mã đang chọn, xóa các ngày giờ và highlight, khôi phục mặc định 20 tuần / 635000 đ; nút Lưu bị khóa đến khi chọn học kỳ. Không tự sinh một mã học kỳ rỗng. Form này tạo cấu hình lần đầu hoặc cập nhật cấu hình cho học kỳ đã có; để tạo học kỳ có mã/tên/năm học mới, dùng quản lý học kỳ tại màn hình Lớp học phần.

POST `/quan-ly-lich-hoc/hoc-ky` kiểm tra đầy đủ ngày giờ, Thứ Hai, khoảng đăng ký, giới hạn số tuần/đơn giá và các tuần đã có lịch; lưu trong transaction Serializable rồi redirect GET để bảng được làm mới. GET Index hiện có luôn truyền đầy đủ Terms và ConfigurationTerm, giữ mã lớp đang điều phối.

POST `/quan-ly-lich-hoc/hoc-ky/xoa` có chống CSRF, yêu cầu Admin; chặn xóa khi còn bất kỳ lớp học phần nào tham chiếu, kể cả lớp đã đóng, để giữ dữ liệu điểm/lịch. Chỉ xóa học kỳ chưa được sử dụng, không cascade xóa lớp. Có xác nhận phía trình duyệt; kiểm tra máy chủ vẫn là quyết định cuối cùng.

Trạng thái dựa vào ngày học, không phải thời hạn đăng ký: trước tuần 1 = Sắp tới; trong SoTuan × 7 ngày = Hiện tại; sau ngày cuối = Đã kết thúc. Chưa có ngày tuần 1 = Chưa cấu hình, không suy đoán trạng thái từ tên hoặc năm học. Đơn giá hiển thị theo nhóm hàng nghìn `635,000 đ`, ngày hiển thị dd/MM/yyyy, giờ đăng ký là giờ Việt Nam.

JavaScript: `wwwroot/js/term-configuration.js`. CSS bổ sung có scope term-toolbar/term-table trong academic.css, không đổi danh mục phòng hoặc bảng điều phối lớp.

Kiểm tra riêng phân hệ (không phụ thuộc các test Portal cũ):

```
dotnet run --project tests/SemesterConfiguration.Checks/SemesterConfiguration.Checks.csproj
node tests/SemesterConfiguration.Checks/term-form.checks.cjs
python tests/SemesterConfiguration.Checks/http.checks.py
```

Kiểm thử SQL tạo/xóa CSDL tạm riêng; kiểm tra HTTP dùng build `.verification/QL_DiemSV.dll`, tự chạy server localhost:5284 và luôn dừng đúng process trong finally. Không dừng dotnet watch đang chạy của người dùng.
