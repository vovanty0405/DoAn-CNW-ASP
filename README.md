# Academic Ledger — Website quản lý điểm sinh viên

Đồ án **Xây dựng website quản lý điểm sinh viên**, phát triển bằng **ASP.NET Core MVC, Entity Framework Core và SQL Server**. Hệ thống kết hợp khu vực quản trị học vụ với Portal dành cho người dùng tra cứu thông tin đào tạo, lịch học và kết quả học tập theo phạm vi được cấp quyền.

Tên hiển thị: **TRƯỜNG ĐẠI HỌC CÔNG NGHỆ & ĐÀO TẠO ACADEMIC LEDGER**.

## Mục lục

- [Chức năng](#chức-năng)
- [Nhóm người dùng](#nhóm-người-dùng)
- [Công nghệ và kiến trúc](#công-nghệ-và-kiến-trúc)
- [Cấu trúc mã nguồn](#cấu-trúc-mã-nguồn)
- [Cài đặt và chạy](#cài-đặt-và-chạy)
- [Quy trình nghiệp vụ](#quy-trình-nghiệp-vụ)
- [Cấu hình email](#cấu-hình-email)
- [Sao lưu và triển khai](#sao-lưu-và-triển-khai)
- [Kiểm tra trước khi sử dụng](#kiểm-tra-trước-khi-sử-dụng)

## Chức năng

### Quản lý đào tạo

- Quản lý hồ sơ sinh viên, giảng viên, khoa, ngành và lớp hành chính.
- Quản lý môn học, số tín chỉ, số tiết lý thuyết/thực hành và môn tiên quyết.
- Quản lý khung chương trình đào tạo theo ngành, phân bổ môn theo học kỳ và quy định xét tốt nghiệp.
- Quản lý học kỳ, lớp học phần, giảng viên phụ trách và sĩ số.
- Xếp sinh viên vào lớp học phần: lọc theo khoa, lớp hành chính, tìm kiếm hoặc nhập danh sách Excel; kiểm tra điều kiện đăng ký khi xác nhận.
- Quản lý phòng học, thiết lập các buổi học và tra cứu thời khóa biểu; kiểm tra xung đột khi lưu lịch.

### Điểm số và phúc khảo

- Nhập điểm theo lớp học phần; cấu hình tỷ lệ quá trình/thi và các cột điểm quá trình.
- Tính điểm tổng kết, điểm chữ và đánh giá kết quả theo quy tắc đang triển khai trong hệ thống.
- Theo dõi điểm thi lần 1, lần 2 và quy tắc giới hạn điểm thi lại theo cấu hình nghiệp vụ.
- Nhập/xuất bảng điểm Excel; có bước xem trước và xác nhận trước khi áp dụng dữ liệu nhập.
- Khóa sổ điểm, yêu cầu mở khóa và xử lý yêu cầu theo quyền.
- Gửi, tiếp nhận và xử lý đơn khiếu nại/phúc khảo điểm; ghi nhận phản hồi và thông báo.
- Tra cứu kết quả theo học kỳ, GPA tích lũy và đối chiếu điều kiện tốt nghiệp.
- Xuất PDF bảng điểm cá nhân hoặc nhiều sinh viên trong phạm vi được phép.

### Portal và quản trị

- Portal hiển thị bài viết, thông báo, chương trình đào tạo, lịch phòng học và thời khóa biểu.
- Tra cứu kết quả học tập có xác thực và kiểm soát phạm vi sinh viên.
- Quản lý tài khoản, nhóm quyền, ma trận chức năng/hành động và trạng thái tài khoản.
- Nhật ký hoạt động, thông báo trên website và thống kê/báo cáo học vụ.
- Gửi email thông báo học vụ qua SMTP khi được cấu hình.

## Nhóm người dùng

| Nhóm quyền | Vai trò chính |
| --- | --- |
| `Admin` | Quản trị hệ thống, tài khoản, quyền và dữ liệu nghiệp vụ. |
| `GiangVien` | Quản lý điểm và phúc khảo của lớp được phân công; tra cứu sinh viên lớp chủ nhiệm theo phạm vi. |
| `SinhVien` | Xem thông tin, thời khóa biểu, kết quả cá nhân và thực hiện nghiệp vụ phúc khảo được phép. |
| `KhaoThi` | Tra cứu kết quả trên phạm vi rộng và xử lý các nghiệp vụ điểm theo quyền được cấp. |
| `GiaoVuKhoa` | Tra cứu sinh viên trong phạm vi khoa được phân công. |
| `DaoTao` | Quản lý nội dung bài viết, thông báo và chương trình đào tạo theo quyền. |

Quyền thực tế còn phụ thuộc ma trận phân quyền, hồ sơ liên kết và điều kiện kiểm tra tại máy chủ. Việc hiển thị một menu không thay thế việc kiểm tra quyền khi gọi chức năng.

## Công nghệ và kiến trúc

| Thành phần | Công nghệ |
| --- | --- |
| Nền tảng | .NET 8 / ASP.NET Core MVC |
| Truy cập dữ liệu | Entity Framework Core SQL Server 8.0.10 |
| Cơ sở dữ liệu | Microsoft SQL Server |
| Giao diện | Razor Views, CSS và JavaScript |
| Xác thực | Cookie Authentication và kiểm tra quyền phía máy chủ |
| Mật khẩu | BCrypt.Net-Next 4.0.3 |
| Kết xuất PDF | QuestPDF 2024.10.2 |
| Email | SMTP, xử lý nền qua `BackgroundService` |

Luồng xử lý chính: **Trình duyệt → Controller → xử lý nghiệp vụ/Service → ApplicationDbContext → SQL Server**. Controller trả về Razor View, JSON hoặc tệp xuất tùy chức năng.

## Cấu trúc mã nguồn

```text
.
├── QL_DiemSV/
│   ├── Controllers/       # Điểm vào MVC/API, kiểm tra quyền và điều phối nghiệp vụ
│   ├── Data/              # DbContext, khởi tạo và nâng cấp cấu trúc dữ liệu
│   ├── Models/            # Các thực thể dữ liệu
│   ├── Services/          # Xử lý điểm, phạm vi tra cứu, PDF, email...
│   ├── ViewModels/        # Dữ liệu phục vụ giao diện và kết xuất
│   ├── Views/             # Razor Views cho quản trị và Portal
│   ├── wwwroot/           # Tài nguyên giao diện
│   ├── Properties/        # Hồ sơ chạy ứng dụng
│   ├── Program.cs         # Đăng ký dịch vụ và cấu hình HTTP pipeline
│   ├── appsettings.json   # Cấu hình cơ bản, không chứa mật khẩu SMTP
│   └── QL_DiemSV.csproj
├── Database_QLDiemSV_Full.sql
├── .gitignore
└── README.md
```

Các thư mục tài liệu, công cụ nội bộ, `scripts`, `outputs`, `output` và kết quả kiểm tra được bỏ qua theo `.gitignore`; chúng không phải thành phần bắt buộc của bản mã nguồn công khai.

## Cài đặt và chạy

### 1. Chuẩn bị

- .NET SDK 8.
- SQL Server (có thể dùng SQL Server Express).
- SQL Server Management Studio hoặc công cụ tương đương để chạy SQL.
- Visual Studio có workload ASP.NET hoặc trình soạn thảo hỗ trợ .NET.

### 2. Lấy mã nguồn

```powershell
git clone https://github.com/vovanty0405/DoAn-CNW-ASP.git
cd DoAn-CNW-ASP
dotnet restore QL_DiemSV/QL_DiemSV.csproj
```

### 3. Chuẩn bị cơ sở dữ liệu

Mở `Database_QLDiemSV_Full.sql`, đọc phần đầu để kiểm tra tên cơ sở dữ liệu và lệnh khởi tạo, sau đó chạy trên SQL Server dùng cho đồ án. Chỉ dùng cơ sở dữ liệu thử nghiệm khi nạp dữ liệu mẫu; sao lưu trước khi chạy trên cơ sở dữ liệu đang sử dụng.

Cấu hình mặc định hiện trỏ tới instance `.\SQLEXPRESS01`, cơ sở dữ liệu `QuanLyDiemSV_Demo`. Hãy thay bằng instance và tên cơ sở dữ liệu thực tế của máy bạn. Có thể lưu cấu hình riêng bằng User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=QuanLyDiemSV_Demo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True" --project QL_DiemSV
```

Chuỗi trên dành cho môi trường phát triển cục bộ. Khi triển khai thật, cấu hình chứng chỉ/kết nối mã hóa phù hợp và dùng tài khoản cơ sở dữ liệu có quyền cần thiết.

Ứng dụng có bước `EnsureCreated`, nâng cấp cấu trúc và khởi tạo dữ liệu khi chạy. Đây không phải quy trình migration tổng quát cho mọi cơ sở dữ liệu cũ; cần kiểm tra log và bản sao dữ liệu trước khi nâng cấp. Nếu có kết quả trùng cặp sinh viên–lớp học phần, phải đối chiếu dữ liệu trước khi tạo ràng buộc duy nhất, không tự xóa điểm.

### 4. Build và chạy

```powershell
dotnet build QL_DiemSV/QL_DiemSV.csproj
dotnet run --project QL_DiemSV/QL_DiemSV.csproj --launch-profile http
```

- Địa chỉ cục bộ: `http://localhost:5167`.
- Portal: `http://localhost:5167/portal`.
- Đăng nhập: `http://localhost:5167/login`.

Tài khoản phụ thuộc bộ dữ liệu được khởi tạo hoặc nhập vào. Không công bố mật khẩu sử dụng thật trong README; đổi mật khẩu tài khoản mẫu trước khi đưa ứng dụng ra ngoài môi trường thử nghiệm.

## Quy trình nghiệp vụ

### Quản lý điểm và công bố

1. Chuẩn bị môn học, học kỳ, lớp học phần, giảng viên và danh sách sinh viên.
2. Cấu hình các thành phần điểm và tỷ trọng.
3. Nhập trực tiếp hoặc nhập Excel, xem trước dữ liệu rồi xác nhận.
4. Lưu bảng điểm theo yêu cầu xác thực của giao diện; kiểm tra điểm tổng kết và điểm chữ.
5. Khóa sổ sau khi đã rà soát dữ liệu.
6. Tra cứu trên Portal hoặc kết xuất bảng điểm PDF.

**Phân biệt điểm quản trị và điểm công bố:** khu vực quản lý điểm vẫn cho người có quyền xem sổ đang nhập. Luồng tra cứu kết quả và PDF chỉ lấy kết quả của lớp học phần có `TrangThaiNhapDiem = "Đã khóa"`. Sổ đang nhập hoặc chờ mở khóa không được lấy vào kết quả tra cứu này. Các sổ lịch sử đã khóa vẫn được hiển thị bình thường.

`TranscriptQueryService` đọc dữ liệu điểm từ cơ sở dữ liệu ở mỗi yêu cầu, không dùng lại cache bảng điểm giữa các yêu cầu. Các thành phần khác như thống kê hoặc kiểm tra phiên đăng nhập vẫn có cơ chế cache riêng.

### Nhập Excel

1. Xuất tệp từ đúng màn hình để lấy cấu trúc cột tương ứng.
2. Giữ nguyên tên cột; nhập mã, CCCD và số điện thoại ở dạng văn bản để bảo toàn số 0 đầu.
3. Chọn tệp nhập, đọc bảng xem trước và thống kê số dòng.
4. Kiểm tra dòng trùng, dữ liệu sai hoặc thông tin đã tồn tại theo từng chức năng.
5. Xác nhận để áp dụng hoặc hủy để dừng thao tác.
6. Kiểm tra phản hồi từ máy chủ và tải lại danh sách sau khi hoàn tất.

Nhập hồ sơ mới và cập nhật bảng điểm có mục đích khác nhau; cần đọc thông báo của từng màn hình, không mặc định rằng mọi tệp nhập đều là thêm mới.

### Phúc khảo

Sinh viên gửi yêu cầu theo dữ liệu được phép truy cập. Người phụ trách xử lý theo phân công và trạng thái sổ điểm. Sau khi điều chỉnh, cần kiểm tra lại kết quả, trạng thái công bố và thông báo liên quan.

## Cấu hình email

Email hiện gửi đến **một địa chỉ nhận cấu hình sẵn** (`AcademicEmail:Recipient`), phù hợp trình diễn khi email trong dữ liệu mẫu không có thật. Địa chỉ nhận có thể khác địa chỉ gửi.

```powershell
dotnet user-secrets set "AcademicEmail:Enabled" "true" --project QL_DiemSV
dotnet user-secrets set "AcademicEmail:Host" "smtp.gmail.com" --project QL_DiemSV
dotnet user-secrets set "AcademicEmail:Port" "587" --project QL_DiemSV
dotnet user-secrets set "AcademicEmail:Username" "sender@example.com" --project QL_DiemSV
dotnet user-secrets set "AcademicEmail:AppPassword" "YOUR_APP_PASSWORD" --project QL_DiemSV
dotnet user-secrets set "AcademicEmail:Recipient" "receiver@example.com" --project QL_DiemSV
```

Thay các giá trị ví dụ bằng thông tin của bạn. Dùng mật khẩu ứng dụng của nhà cung cấp SMTP, không dán bí mật vào mã nguồn hoặc Git. User Secrets dùng cho môi trường phát triển; môi trường triển khai cần biến môi trường hoặc kho bí mật riêng.

Email chỉ thông báo có cập nhật học vụ và yêu cầu đăng nhập để xem chi tiết. Hàng đợi email hiện nằm trong bộ nhớ, chưa phải hàng đợi bền vững; không dùng email làm bằng chứng duy nhất rằng nghiệp vụ đã hoàn tất. Khi gửi lỗi, kiểm tra log và thông báo trong website.

## Sao lưu và triển khai

- Sao lưu SQL Server định kỳ và lưu bản sao ở vị trí tách biệt với máy chạy ứng dụng.
- Lưu cả tệp đính kèm cần thiết, cấu hình triển khai và khóa bảo vệ dữ liệu khi lập kế hoạch phục hồi.
- Kiểm thử khôi phục trên môi trường riêng; việc tạo được tệp backup chưa chứng minh phục hồi thành công.
- Các script sao lưu nội bộ được chủ dự án giữ cục bộ và không đưa vào repository này. Bản clone công khai không bao gồm sẵn quy trình backup đám mây tự động.
- Khi triển khai, dùng HTTPS, cấu hình host, giới hạn quyền SQL Server, quản lý bí mật riêng và kiểm tra quyền truy cập các tệp tải lên.
- Không đưa dữ liệu cá nhân thật, tệp Excel chứa hồ sơ sinh viên hoặc bản backup lên repository công khai.

## Kiểm tra trước khi sử dụng

Các nội dung cần kiểm tra trên môi trường đích:

- Đăng nhập, đăng xuất, tài khoản bị vô hiệu hóa và truy cập sai quyền.
- CRUD và kiểm tra mã, CCCD, số điện thoại, email theo quy tắc của từng màn hình.
- Nhập Excel gồm dòng mới, dòng trùng, dòng lỗi và thao tác hủy xem trước.
- Tính điểm, thi lại, khóa/mở sổ và công bố kết quả trên Portal/PDF.
- Phạm vi lớp giảng dạy, lớp chủ nhiệm và khoa được phân công.
- Xử lý phúc khảo và thông báo/email sau cập nhật.
- Kiểm tra xung đột lịch, điều kiện xếp lớp và giới hạn sĩ số.
- Khôi phục dữ liệu từ bản sao lưu và hiệu năng với khối lượng dữ liệu thực tế.

Đây là dự án đồ án; việc build thành công không đồng nghĩa toàn bộ tình huống nghiệp vụ hoặc mức tải triển khai đã được kiểm chứng. Kết quả đánh giá cần dựa trên lần kiểm thử cụ thể.

## Quản lý phiên bản

`scripts/`, `script/`, `outputs/`, `output/`, tài liệu nội bộ và tệp build được bỏ qua theo `.gitignore`. Các tệp đã được theo dõi từ trước phải được bỏ khỏi Git index thì quy tắc bỏ qua mới có hiệu lực; tệp cục bộ vẫn có thể được giữ lại.

Repository chỉ cung cấp mã nguồn và tài nguyên nằm trong phạm vi commit. Việc bỏ theo dõi một tệp không xóa nó khỏi lịch sử commit trước đó.
