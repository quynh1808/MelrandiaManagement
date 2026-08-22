# MelrandiaManagement

MelrandiaManagement là website trung tâm của hệ sinh thái Melrandia. Ứng dụng giới thiệu các dự án độc lập, cung cấp một điểm truy cập chung và sẽ phát triển thành Console quản trị, giám sát tổng hợp trong các giai đoạn sau.

## Phạm vi hiện tại

- Trang chủ tiếng Việt, render tĩnh phía server.
- Header desktop là một cụm flex thu gọn, căn giữa, gồm ảnh nhận diện, `Melrandia`, menu, icon theme, `VI/EN` và Đăng nhập.
- Menu thả xuống Dự án và Bài viết hỗ trợ hover, focus bàn phím và chạm trên màn hình cảm ứng.
- Theme dùng cùng bảng màu xanh dương–cyan của AMS; nút theme/ngôn ngữ có icon và animation.
- Nút Đăng nhập luôn dùng màu nhấn tương phản với theme hiện tại.
- `EN` chỉ là trạng thái chuẩn bị, chưa có nội dung tiếng Anh.
- Giao diện sáng/tối lưu bằng `localStorage` và có cookie dự phòng nên không reset khi tải lại trang.
- Dark mode dùng chữ trắng hoặc gần trắng cho điều hướng và nội dung phụ để tăng khả năng đọc.
- Hiệu ứng xuất hiện nội dung khi cuộn và hỗ trợ `prefers-reduced-motion`.
- Đồ họa nội dung dùng HTML/CSS; `wwwroot/images/avatar.png` là ảnh duy nhất, chỉ phục vụ nhận diện thương hiệu.
- Route riêng cho Giới thiệu, Dự án, Bài viết, Liên hệ và Đăng nhập.
- AMS và Agriculture System được đăng ký dưới dạng metadata công khai, không tham chiếu source hoặc database của dự án con.
- Ba route chuyên mục Bài viết: Kinh tế, Công nghệ và Giới thiệu sách.
- Health endpoint `/health/live` và `/health/ready`.
- Hỗ trợ tạo OCI image bằng `.NET SDK PublishContainer`.

Authentication, quản trị nội dung, Project Registry bền vững, theo dõi health AMS và SSO chưa thuộc phạm vi phiên bản này.

## Quan hệ với AMS

Hai repository nằm ngang hàng và có vòng đời độc lập:

```text
MelrandiaSystems/
├── AquacultureMonitoringSystem/
└── MelrandiaManagement/
```

`PublicProjectCatalog` chỉ giữ thông tin giới thiệu AMS. Nó không đọc MongoDB, MQTT, cảm biến hoặc cấu hình của AMS. Về sau implementation này sẽ được thay bằng Project Registry trong database của MelrandiaManagement.

## Chạy trên Windows

Yêu cầu .NET SDK được pin trong `global.json`.

```powershell
cd C:\Users\ranca\Documents\MelrandiaSystems\MelrandiaManagement
dotnet restore --locked-mode
dotnet build MelrandiaManagement.sln --configuration Release --no-restore
dotnet test MelrandiaManagement.sln --configuration Release --no-build
dotnet run --project MelrandiaManagement.csproj
```

Mặc định Visual Studio/PlatformIO launch profile mở `http://localhost:5106`.

## Cấu trúc quan trọng

| Thành phần | Mục đích | Vai trò |
|---|---|---|
| `Components/Layout/PublicLayout.razor` | Khung trang công khai | Đặt header, nội dung route và footer vào cùng cấu trúc |
| `Components/Pages/Home.razor` | Trang chủ | Giới thiệu Melrandia bằng các teaser, không chứa nội dung chi tiết của trang con |
| `Components/Shared/PublicHeader.razor` | Header dùng chung | Điều hướng, theme, ngôn ngữ và lối vào đăng nhập |
| `Components/Shared/ProjectConsoleGraphic.razor` | Đồ họa hero | Tạo minh họa quản lý dự án bằng HTML, không dùng ảnh |
| `Services/PublicProjectCatalog.cs` | Catalog dự án tạm thời | Cô lập metadata dự án khỏi UI và chuẩn bị cho persistence |
| `Services/PublicArticleCatalog.cs` | Catalog chuyên mục tạm thời | Giữ URL chuyên mục ổn định trước khi có CMS |
| `wwwroot/images/avatar.png` | Ảnh nhận diện | Hiển thị bên trái wordmark; không chứa dữ liệu nghiệp vụ |
| `wwwroot/app.css` | Design system | Theme, Times New Roman, responsive, layout và animation |
| `wwwroot/js/theme-init.js` | Khởi tạo theme | Áp dụng theme trước khi trang vẽ để hạn chế chớp màu |
| `wwwroot/js/site.js` | Progressive enhancement | Theme toggle, menu mobile, thông báo EN và scroll reveal |

## Nguyên tắc nội dung công khai

Trang công khai không được đưa dữ liệu cảm biến, cảnh báo, địa chỉ MQTT, database, secret hoặc chi tiết vận hành AMS vào HTML. Thông tin chưa được xác nhận như email và địa chỉ liên hệ không được tự tạo.

## Tài liệu đọc tiếp

1. [Thiết kế trang chủ](Docs/01.PUBLIC-HOME.md)
2. [Kiến trúc và ranh giới](Docs/02.ARCHITECTURE.md)
3. [Báo cáo kiểm tra](Docs/03.VALIDATION.md)
4. [Hướng dẫn Git và push GitHub](Docs/04.GIT-PUSH.md)
