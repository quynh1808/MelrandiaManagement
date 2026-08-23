# Tests

## Mục đích

Test project bảo vệ các contract có thể gây lỗi bảo mật hoặc dữ liệu nếu bị thay đổi âm thầm. Test không dùng database production và không cần PostgreSQL đang chạy.

`ManagementPersistenceTests` đọc EF model metadata để xác nhận unique key/slug, khóa project access và quan hệ category/article/media; đồng thời kiểm tra validator từ chối database password trống và chu kỳ health quá ngắn. `PublicArticleCatalogTests` xác nhận chỉ bài Published hoặc Scheduled đã tới hạn được public, Draft bị ẩn và Markdown scheme nguy hiểm bị từ chối. `ArticlePublishingTests` lưu một draft cùng ảnh PNG hợp lệ vào database/file storage tạm rồi kiểm tra cả metadata lẫn byte file.

Chạy kiểm thử:

```powershell
dotnet test MelrandiaManagement.sln --configuration Release --no-build --no-restore
```

Model test không thay thế integration test. Database thật được kiểm tra bằng `dotnet ef database update`, startup migration và `/health/ready` theo `Docs/05.POSTGRESQL-SETUP.md`.
