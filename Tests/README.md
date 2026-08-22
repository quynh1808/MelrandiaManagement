# Tests

Project MSTest bảo vệ các contract nhỏ nhưng quan trọng của MelrandiaManagement. `PublicProjectCatalogTests` xác nhận AMS là project công khai đầu tiên, Agriculture System tồn tại, slug không phân biệt hoa thường và project chưa định nghĩa không bị tự tạo. `PublicArticleCatalogTests` bảo vệ thứ tự ba chuyên mục và khả năng tra cứu slug.

Chạy kiểm thử:

```powershell
dotnet test MelrandiaManagement.sln --configuration Release
```

Integration test cho authentication, database và project health sẽ được thêm cùng các capability tương ứng; không tạo mock cho chức năng chưa tồn tại.
