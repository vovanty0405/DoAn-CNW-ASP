# Cổng thông tin Đào tạo — Academic Ledger

## Truy cập

- `/portal`: trang công khai với header, 6 truy cập nhanh, bài viết theo danh mục, tin tức, lịch phòng hôm nay, chương trình đào tạo, tài liệu và liên hệ.
- `/portal/tra-cuu-tkb`: chọn sinh viên/giảng viên, nhập mã, học kỳ và tuần. Chỉ hiển thị họ tên/lớp/ngành hoặc khoa và các buổi lịch tương ứng; không đưa điểm hay thông tin liên hệ cá nhân vào kết quả.
- `/portal/chuong-trinh/{MaNganh}`: các môn, tín chỉ và học kỳ dự kiến từ KhungChuongTrinh.
- `/bai-viet`: quản lý bài viết cho Admin/GiangVien. Giảng viên chỉ xem/sửa/xóa/ghim bài của mình, Admin quản lý tất cả.
- `/bai-viet/them`: soạn thảo Rich Text, chọn danh mục/đơn vị/trạng thái, ghim, ảnh thumbnail và tệp đính kèm.
- `/portal/bai-viet/{Id}`: bài xuất bản công khai; bản nháp chỉ tác giả hoặc Admin xem được.

Trang `/` dẫn khách chưa đăng nhập, sinh viên và giảng viên đến Portal. Admin vẫn giữ trang tổng quan quản trị. Menu Portal và quản trị đều có liên kết đến quản lý bài viết cho người có quyền.

## Luồng xuất bản

1. Đăng nhập bằng tài khoản Admin/Giảng viên.
2. Vào Bài viết & Thông báo → Thêm bài viết mới.
3. Nhập tiêu đề, tóm tắt, đơn vị đăng, nội dung, danh mục và chọn Bản nháp hoặc Đã xuất bản.
4. Khi xuất bản, bài xuất hiện ngay trên Portal. Danh mục Tin tức & Sự kiện đồng thời xuất hiện trong vùng tin có ảnh.
5. Nhãn MỚI hiển thị trong 72 giờ từ thời điểm xuất bản. Ghim được ưu tiên trước ngày đăng. Chỉnh sửa bài đã xuất bản giữ nguyên ngày đăng.

Rich Text có đậm/nghiêng/gạch chân, danh sách và liên kết. Server dùng bộ lọc HTML theo allowlist, loại script, sự kiện, style và URL nguy hiểm. HTML không hợp lệ được mã hóa thành văn bản để hiển thị an toàn. Trình biên tập paste dưới dạng plain text. Có textarea HTML dự phòng khi JavaScript bị tắt.

Tệp tối đa 5 MB; hỗ trợ PDF/DOCX/XLSX/TXT/PNG/JPG/JPEG, có kiểm tra chữ ký cơ bản. Tệp nằm trong `App_Data/PortalFiles`, ngoài wwwroot; chỉ tải qua endpoint có kiểm tra trạng thái xuất bản/quyền tác giả. Filename vật lý dùng GUID. Xóa/gỡ/thay tệp sẽ dọn tệp cũ; nếu tệp bị một download giữ khóa, việc dọn có thể cần thực hiện sau. Cần sao lưu thư mục này cùng CSDL.

## Dữ liệu và mở rộng

`Article` lưu người đăng bằng UserID lấy từ cookie claims; không nhận ID tác giả từ form. Các thao tác ghi đều có chống CSRF và kiểm tra quyền trên máy chủ.

`PortalDocument` là cấu trúc sẵn cho quản lý tài liệu về sau: TieuDe, Loai (`QuyChe` hoặc `BieuMau`), FileUrl, DungLuongBytes, HienThi. Portal chỉ hiển thị bản ghi HienThi=true và URL hợp lệ. Chưa tạo màn hình CRUD tài liệu theo phạm vi yêu cầu. Ban đầu không tạo các link tải giả; khu vực trống ghi đang cập nhật.

`Nganh.HeDaoTao` dùng để nhóm hệ. CSDL cũ được bổ sung mặc định Chính quy. Khi dữ liệu có hệ Từ xa hoặc các hệ khác, Portal tự nhóm theo giá trị thực.

Thông tin liên hệ cấu hình tại appsettings hoặc environment:

```json
"Portal": {
  "DiaChi": "Địa chỉ thực tế của trường",
  "Hotline": "Số điện thoại Phòng Đào tạo",
  "Email": "Email liên hệ thực tế"
}
```

Không cấu hình sẽ hiển thị thông báo đang cập nhật, không bịa thông tin liên hệ. Không seed bài/tài liệu giả vào CSDL hiện có.

`AcademicSchemaUpgrade.Apply` bổ sung bảng Article/PortalDocument và cột HeDaoTao khi ứng dụng khởi động, giữ nguyên dữ liệu trước đó. Chạy lại an toàn. Không dùng EF migration cho dự án đang dùng EnsureCreated.

Widget lịch phòng đọc tuần/ngày theo giờ Việt Nam, hiển thị các tiết có lịch trong ngày; trạng thái Đang học chỉ khi thời gian hiện tại nằm trong một tiết đã đặt. Ngoài tiết học hoặc phòng không có buổi đang diễn ra hiển thị Trống; chưa có học kỳ hiện hành hiển thị Chưa có lịch. Phòng bảo trì/khóa giữ đúng trạng thái. Widget cập nhật khi tải lại trang.

Ma trận phòng có thể xem công khai qua `/quan-ly-lich-hoc/ma-tran`, nhưng các action sửa lịch/cấu hình/phòng vẫn yêu cầu Admin. Khi khách/giảng viên/sinh viên xem ma trận, dùng layout Portal và không có nút sửa phòng/lịch.

## Giao diện

Layout riêng `_PortalLayout.cshtml`, CSS `portal.css`, JS `portal.js`. Mặc định Sáng khi chưa có tùy chọn; nút Sáng/Tối dùng cùng khóa `app-theme` trong localStorage với hệ thống quản trị. Responsive theo màn hình, header xuống dòng, quick cards 6 → 3 cột, vùng nội dung 2/3–1/3 → một cột ở màn hình nhỏ.

Thumbnail nhận đường dẫn `/images/...` hoặc URL HTTPS; mặc định dùng hình SVG cục bộ `/images/portal-news.svg`. Nội dung ảnh ngoài được lấy từ URL do người biên tập nhập. Phần biên tập dùng CSS Dark Mode có sẵn trong khu vực quản trị.

## Kiểm tra

- `dotnet build QL_DiemSV/QL_DiemSV.csproj --no-restore`
- `dotnet run --project tests/AcademicLedger.Checks/AcademicLedger.Checks.csproj -- --database`: CSDL tạm riêng; kiểm tra cả phân hệ cũ và Portal, quyền bài viết, ẩn nháp, sanitization, tệp đính kèm, tra cứu TKB. CSDL và tệp thử được xóa sau kiểm thử.
- `python tests/AcademicLedger.Checks/portal_smoke.py http://localhost:5282`: khi có phiên ứng dụng ở cổng tương ứng, dùng tài khoản seed để kiểm tra HTTP. Không tạo bài công khai; đăng nhập ghi session/nhật ký như bình thường.

Các phiên kiểm tra phải dừng sau sử dụng. Không để tiến trình thử dùng chung DLL với `dotnet watch`; có thể build sang `.verification` để kiểm tra khi ứng dụng người dùng đang chạy.

## Tra cứu TKB — luồng hai bước

1. Mở `/portal/tra-cuu-tkb`: chỉ có đối tượng, mã và nút tra cứu. Không truy vấn danh sách học kỳ/lịch khi chưa tìm thấy hồ sơ. Mã sai hiển thị lỗi và giữ trạng thái tìm kiếm.
2. Sau khi tìm thấy hồ sơ: lấy học kỳ/tuần theo ngày hiện tại (giờ Việt Nam), hiển thị bộ lọc, banner và TKB. Không có học kỳ hiện hành thì báo rõ và cho chọn học kỳ gần nhất, không giả định một học kỳ cũ là hiện tại.

Đổi Học kỳ, Tuần hoặc chế độ xem tự submit GET, giữ code/kind trên URL. Đổi học kỳ tính lại tuần; các tuần được clamp trong 1..SoTuan. Xem toàn học kỳ hiển thị danh sách tất cả buổi và tuần học thay vì bỏ sót các tuần ngoài tuần đang chọn. Tra cứu mã khác trở về bước 1.

Banner SV lấy NgaySinh, MaLop và Nganh/Khoa qua LopHanhChinh. Banner GV lấy HocVi, HoTen, Khoa, Email. Giá trị thiếu ghi Chưa cập nhật, không điền dữ liệu giả.

Lưới tuần gồm 9 cột: Tiết bên trái, 7 ngày, Tiết bên phải; header/footer đủ bảy ngày. Tiết 1–12 kèm giờ 50 phút. Gộp dọc bằng rowspan và bỏ TD ở vùng đã gộp. Trường hợp dữ liệu có trùng giờ, các lịch đều được giữ trong khối giao nhau và mỗi lịch có thẻ trạng thái riêng.

Màu tính tại TimetablePresentation:
- Bình thường: màu xanh chuẩn Portal.
- Trùng một phần: xanh lá; ít nhất một tiết thực sự giao nhau.
- Trùng toàn bộ: tím; mọi tiết của lịch được bao phủ bởi lịch khác trong các tuần đang xét. Toàn học kỳ xét cả giao nhau về tuần.
- Học kỳ cũ: vàng khi ngày kết thúc tuần cuối < hoặc = hôm nay và không có trạng thái ưu tiên hơn. Học kỳ chưa có ngày tuần 1 không bị tự đoán là cũ.
- Báo nghỉ: hồng, dựa trên ClassSchedule.TuanBaoNghi. Admin nhập danh sách tuần nghỉ trong Điều phối lịch (vd 3,5 hoặc 3-5), phải là tập con tuần học. Không suy luận nghỉ từ nội dung bài viết hoặc màu ngẫu nhiên. Một tuần nghỉ không làm các tuần khác bị tô hồng; lịch đã nghỉ không gây cảnh báo trùng với lịch khác trong tuần đó. Toàn học kỳ chỉ tô cả dòng nghỉ khi mọi tuần đều nghỉ; các trường hợp nghỉ một số tuần ghi cụ thể danh sách tuần.

Ưu tiên: báo nghỉ → trùng lịch → học kỳ cũ → bình thường. Trường Tuần báo nghỉ là dữ liệu lịch do Admin cập nhật; chưa tạo quy trình phê duyệt đơn nghỉ riêng.

Bốn nút Tuần Đầu/Trước/Kế/Cuối chỉ hiện trong chế độ tuần và bị vô hiệu ở hai biên. In TKB mở hộp thoại trình duyệt; chọn Save as PDF. CSS A4 ngang giữ màu, ẩn header/footer Portal và bộ lọc, chỉ in hồ sơ, bảng, chú thích và thời gian tải.

Dòng Dữ liệu được cập nhật vào lúc hiển thị thời điểm truy vấn snapshot hiện tại từ máy chủ (LoadedAt), không phải timestamp thay đổi cuối cùng của bản ghi. Endpoint TKB trả Cache-Control: no-store.

Mã nguồn: PortalController.Timetable, PortalTimetableViewModel, TimetablePresentation.cs, Views/Portal/Timetable.cshtml, wwwroot/css/portal-timetable.css, wwwroot/js/portal-timetable.js. Kiểm tra HTTP: `python tests/AcademicLedger.Checks/timetable_smoke.py http://localhost:5283`.
