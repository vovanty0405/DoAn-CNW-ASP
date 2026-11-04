# Chương trình đào tạo

Portal: `/Portal/Curriculums`, `/Portal/CurriculumDetail/{id}`. Trang chủ nhóm hệ/khóa mới nhất/ngành, một chương trình dẫn trực tiếp, nhiều chương trình dùng popover. Danh sách có lọc khóa/chương trình/ngành; 11 accordion chi tiết mặc định đóng.

Admin: `/admin/curriculums`. Thêm/sửa 11 mục; chèn bảng mẫu mục 6; lọc hệ/khóa/ngành/chương trình/trạng thái; sao chép sang năm mới (mặc định ẩn); xóa chỉ xóa nội dung CTĐT đó. Dữ liệu khung môn học cũ không bị thay thế hay xóa.

NganhHocId dùng string tương thích MaNganh hiện có, không dùng int vì sẽ mất quan hệ dữ liệu. Migration cộng thêm chạy qua AcademicSchemaUpgrade như cơ chế hiện có; có unique index tránh trùng hệ/khóa/ngành/chương trình và thứ tự mục.

Trình soạn Rich Text hiện dùng custom contenteditable ribbon đầy đủ, không cần TinyMCE API key. Nội dung được lọc bằng PortalRules.SanitizeHtml khi lưu và hiển thị. Bảng mẫu là khung để Admin nhập tín chỉ/tỷ lệ thực. Bản ẩn không hiển thị Portal; Admin xem trước được.

Kiểm tra dữ liệu riêng: `dotnet run --project tests/Curriculum.Checks/Curriculum.Checks.csproj`. Bộ kiểm thử tạo/xóa CSDL riêng và kiểm tra 11 mục, HTML, trùng khóa, ẩn/công khai, clone độc lập, xóa cascade.

## Ribbon và Sidebar

11 mục dùng custom contenteditable ribbon không cần API key. Có font/cỡ 8–36pt, định dạng chữ, căn lề/dòng, danh sách, lịch sử riêng từng mục, ảnh URL, link, đường kẻ, HTML và bảng. Kẻ bảng bằng lưới 8×8; Ctrl/⌘+click để chọn các ô trong hình chữ nhật rồi Gộp ô. Tách ô, thêm/xóa dòng/cột/bảng, viền và màu nền được lưu qua rowspan/colspan và CSS đã lọc. Bảng mẫu có hàng tiêu đề gộp sẵn. Dán HTML giữ cấu trúc bảng, bỏ mso-* và thành phần hoạt động; không cam kết giữ mọi đặc tính độc quyền của Word như VML hoặc macro.

Clipboard button cần quyền đọc clipboard của trình duyệt; nếu bị từ chối, Ctrl+V trong vùng soạn thảo vẫn xử lý HTML. Ảnh dùng URL HTTP(S) hoặc đường dẫn tài nguyên hợp lệ, không lưu dữ liệu ảnh Clipboard dạng base64. Đồng bộ hai chiều bằng sự kiện input, trước khi submit chuyển HTML sang XML hợp lệ để bộ lọc server giữ cấu trúc. HTML lỗi ở máy chủ được mã hóa thay vì cho qua bằng regex.

Sidebar dùng `_SidebarNavigation.cshtml`, `sidebar.js`, `sidebar.css`: nhóm Portal, Đào tạo, Điểm, Hệ thống; Tổng quan luôn hiển thị. Phân quyền theo vai trò như trước; nhóm chứa trang hiện tại luôn được mở khi tải trang, các nhóm khác nhớ trạng thái localStorage. Chưa có phân hệ quản lý văn bản/biểu mẫu nên không thêm link quản trị chưa tồn tại.

Kiểm tra: `python tests/EditorSidebar.Checks/browser.checks.py` (Chrome headless/profile riêng, 14 kiểm tra tương tác); `dotnet run --project tests/EditorSidebar.Checks/EditorSidebar.Checks.csproj` (lọc HTML và giữ cấu trúc sau lưu).
