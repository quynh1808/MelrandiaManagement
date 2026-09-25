# MelrandiaManagement

MelrandiaManagement (MM) là website và Management Console trung tâm của hệ sinh thái Melrandia. Mỗi dự án con như Aquaculture Monitoring System (AMS) vẫn có repository, container, database và chu kỳ phát hành riêng; MM quản lý danh tính, danh mục dự án, quyền truy cập và điểm mở dự án từ một nơi chung.

## Tính năng hiện có

- Trang công khai tiếng Việt: Trang chủ, Giới thiệu, Dự án, Bài viết và Liên hệ.
- CMS bài viết dùng PostgreSQL: chuyên mục động, lưu nháp, công bố ngay, hẹn giờ UTC, lưu trữ, xem trước, ảnh bìa/thư viện và audit; nội dung Markdown được tắt raw HTML.
- Ảnh bài viết được kiểm tra chữ ký JPEG/PNG/GIF/WebP và lưu trong `App_Data/media`/Docker volume riêng; Git và container image không chứa dữ liệu upload vận hành.
- Theme sáng/tối lưu trong trình duyệt; font Times New Roman; menu responsive và hỗ trợ bàn phím/Reduce Motion.
- Project Registry bền vững trong PostgreSQL riêng của MM; menu và trang công khai đọc dự án từ registry thay vì hard-code.
- Public Project Registry dùng cache 30 giây chống truy vấn trùng giữa header/nội dung; admin lưu project sẽ invalidate cache ngay.
- Đăng nhập bằng ASP.NET Core Identity; password được hash, cookie HttpOnly/SameSite, lockout 15 phút sau 5 lần sai và rate limit endpoint đăng nhập.
- Bootstrap tài khoản `Administrator` đúng một lần khi database chưa có admin; password lấy từ secret hoặc biến môi trường, không commit vào Git.
- Policy `ManagePortal` kiểm tra quyền ở server cho toàn bộ route và endpoint quản trị.
- Trang đăng nhập dùng `AuthLayout` riêng theo bố cục AMS; không render header, điều hướng hay footer của website công khai.
- `/Management` theo bố cục System Management Portal: sidebar, topbar, KPI, bộ lọc `All status`, project card, Recent activity, Alert Overview, System overview và trạng thái vận hành tổng.
- Menu quản trị gồm Dự án, Network & Connections, Nhiệm vụ, Thông báo, Báo cáo, Tích hợp, Tài khoản, Quyền dự án và Thiết lập.
- Systems/Alerts/Reports/Integrations dùng Project Registry, health, latency và audit thật. Tasks hiện là checklist cấu hình tự sinh; các module chưa có persistence được ghi rõ thay vì hiển thị dữ liệu giả.
- Menu tài khoản Administrator dạng dropdown chứa điều hướng cá nhân và form đăng xuất có antiforgery.
- Overlay kết nối Blazor hiển thị `Rejoining the server...`, lần thử hiện tại và số giây chờ khi browser mất kết nối tới server; có nút thử lại/tải lại khi không thể khôi phục phiên.
- Giao diện thêm/chỉnh sửa dự án, tạo tài khoản, cấp/thu hồi quyền Administrator/Operator/Viewer theo từng dự án; các thao tác thay đổi có hộp xác nhận.
- Health monitor gọi URL do admin khai báo, cập nhật `Healthy/Unavailable/Unknown`; lỗi AMS không làm MM ngừng hoạt động.
- Audit log cho bootstrap admin, đăng nhập/đăng xuất, tài khoản, project, quyền và thay đổi health.
- `/health/live` kiểm tra process; `/health/ready` kiểm tra kết nối PostgreSQL.
- EF Core migrations cho Identity, Project Registry, project access, audit và hệ thống bài viết.
- OCI image immutable được tạo bằng .NET SDK `PublishContainer`, không dùng Dockerfile; GitHub Actions chỉ publish tag `sha-*` sau build/test/migration check thành công.
- Docker Compose cho Ubuntu dùng PostgreSQL 17 riêng, volume dữ liệu và volume Data Protection keys.

## Ranh giới với AMS

```text
Browser ──HTTP──> MelrandiaManagement :8090
                    ├── PostgreSQL MM (identity/registry/access/audit)
                    └── HTTP health/link ──> AMS :8080

AMS ──> PostgreSQL AMS / MQTT / PLC / ESP32
```

MM không project-reference AMS, không đọc PostgreSQL AMS, không kết nối MQTT/PLC và không điều khiển thiết bị. `PublicUrl` là URL trình duyệt mở AMS; `HealthUrl` là endpoint MM gọi để quan sát trạng thái. Đây là tích hợp loose coupling: AMS vẫn hoạt động khi MM dừng và ngược lại.

## Chạy lần đầu trên Windows

Yêu cầu: .NET SDK trong `global.json`, PostgreSQL 17 đang chạy và database/role đã được tạo theo [hướng dẫn PostgreSQL](Docs/05.POSTGRESQL-SETUP.md).

```powershell
cd C:\Users\ranca\Documents\MelrandiaSystems\MelrandiaManagement
dotnet tool restore
dotnet restore MelrandiaManagement.sln --locked-mode
dotnet user-secrets set "PostgreSql:Password" "MAT_KHAU_ROLE_MELRANDIA_APP"
dotnet user-secrets set "BootstrapAdmin:Password" "MAT_KHAU_ADMIN_DU_12_KY_TU"
dotnet build MelrandiaManagement.sln --configuration Release --no-restore
dotnet test MelrandiaManagement.sln --configuration Release --no-build --no-restore
dotnet run --project MelrandiaManagement.csproj
```

Mở `http://localhost:5106/login`. Đăng nhập bằng username `admin` và password bootstrap đã đặt. Khi thành công, server chuyển tới `/Management`. Secret bootstrap không đổi password của account đã tồn tại; nếu cần recovery, dùng command có audit trong [Docs 05](Docs/05.POSTGRESQL-SETUP.md#đổi-bootstrap-password-nhưng-vẫn-không-đăng-nhập-được), không xóa bảng Identity.

## Cấu hình quan trọng

| Cấu hình | Vai trò | Production |
|---|---|---|
| `PostgreSql__*` | Kết nối database riêng `melrandia_management` | Đặt qua `.env`/secret |
| `BootstrapAdmin__*` | Tạo admin duy nhất ở database mới | Password bắt buộc, không commit |
| `InitialProjects__AmsPublicUrl` | URL trình duyệt mở AMS | Dùng IP/hostname LAN thật |
| `InitialProjects__AmsHealthUrl` | URL container MM gọi để kiểm tra AMS | Không dùng `localhost` nếu khác container |
| `DataProtection__KeysPath` | Giữ key giải mã cookie qua restart/redeploy | Mount volume bền vững |
| `ArticleMedia__RootPath` | Nơi lưu byte ảnh bài viết | `/app/App_Data/media` trên volume riêng |
| `ArticleMedia__MaxFileSizeMegabytes` | Giới hạn mỗi ảnh | Mặc định 8 MB |
| `ArticleMedia__MaxFilesPerArticle` | Giới hạn file trong một lần lưu | Mặc định 8 |

## Route quản trị

| Route | Chức năng |
|---|---|
| `/login` | Đăng nhập account trung tâm |
| `/Management` | Dashboard tổng hợp |
| `/Management/Projects` | Project Registry và URL/health |
| `/Management/Systems` | Network, public endpoint, health endpoint, latency và trạng thái connection |
| `/Management/Tasks` | Checklist cấu hình tự sinh từ trạng thái project |
| `/Management/Alerts` | Cảnh báo health/configuration đang tồn tại |
| `/Management/Reports` | Tổng hợp health, coverage, latency và audit gần đây |
| `/Management/Integrations` | Catalog PostgreSQL và HTTP integration của các project |
| `/Management/Articles` | Soạn, lưu nháp, hẹn giờ, công bố, lưu trữ và chỉnh sửa bài viết |
| `/Management/Users` | Tạo tài khoản central identity |
| `/Management/Access` | Cấp/thu hồi role theo dự án |
| `/Management/Settings` | Mô tả ranh giới cấu hình và database |

## Tài liệu theo thứ tự đọc

1. [Trang chủ công khai](Docs/01.PUBLIC-HOME.md)
2. [Kiến trúc và ranh giới](Docs/02.ARCHITECTURE.md)
3. [Báo cáo kiểm tra](Docs/03.VALIDATION.md)
4. [Git và push GitHub](Docs/04.GIT-PUSH.md)
5. [Thiết lập PostgreSQL Windows/Ubuntu](Docs/05.POSTGRESQL-SETUP.md)
6. [Đăng nhập, phân quyền và Management Console](Docs/06.AUTH-MANAGEMENT.md)
7. [Hướng dẫn source code và vai trò file](Docs/07.SOURCE-CODE-GUIDE.md)
8. [Khuôn mẫu hiệu năng và logging](Docs/08.PERFORMANCE-LOGGING.md)
9. [Thiết kế Management Console và nguồn dữ liệu UI](Docs/09.MANAGEMENT-FRONTEND.md)
10. [Hệ thống biên soạn và xuất bản bài viết](Docs/10.ARTICLE-PUBLISHING.md)

Không commit `.env`, password, token GHCR, private key hoặc certificate. `.env.example` chỉ là mẫu tên biến và bắt buộc thay toàn bộ placeholder trước khi chạy.



command run: dotnet run --PostgreSql:Password=password for SQL