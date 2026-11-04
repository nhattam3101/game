# Cài đặt và chạy trên Fedora/Linux

Dự án khóa ở Unity `6000.6.6f1`. Unity Editor không chạy ngầm chỉ vì repository
đang tồn tại; nó chỉ chạy khi bạn mở Editor/Hub hoặc tự cấu hình batch job.

## Cài Unity Hub trên Fedora

Unity chính thức hỗ trợ một số distro Linux cụ thể; Fedora vẫn thường chạy được
nhưng có thể cần xử lý dependency/Wayland theo phiên bản hệ điều hành. Ưu tiên
hướng dẫn Linux mới nhất tại:

- <https://docs.unity3d.com/hub/manual/InstallHub.html>
- <https://unity.com/releases/editor/archive>

Sau khi cài Hub:

1. Đăng nhập Unity ID và kích hoạt license cá nhân nếu phù hợp.
2. Trong **Installs**, chọn **Install Editor**.
3. Nếu `6000.6.6f1` không xuất hiện, mở Unity Download Archive, chọn đúng phiên
   bản và bấm **Unity Hub**.
4. Cài thêm **Linux Build Support (Mono)** nếu cần xuất bản build Linux.
5. Add project from disk và chọn thư mục repository.

Không mở dự án bằng một phiên bản khác rồi lưu scene/prefab, vì Unity có thể
migrate asset và tạo diff lớn khó rollback.

## Lần chạy đầu

Unity sẽ resolve package và tạo `Library/`; bước này có thể dùng nhiều CPU, RAM,
disk và kết nối mạng. Đây là hoạt động import/compile bình thường và dừng khi bạn
đóng Editor. Có thể kiểm tra process đang chạy bằng:

```bash
pgrep -a -f 'Unity|UnityHub|UnityPackageManager'
```

Sau khi import:

1. Mở Console và xử lý compile error nếu có.
2. Chạy **Tools > MonsterSurvival > QA > Core Gameplay Audit**.
3. Mở `Assets/MonsterSurvival/Content/Scenes/MainMenu.unity` và bấm Play.

## Lỗi thường gặp

- **Hub không thấy Editor đúng phiên bản:** cài từ Unity Download Archive thay vì
  chọn phiên bản gần nhất.
- **Cửa sổ đen/flicker trên Wayland:** đăng xuất và thử phiên X11, hoặc chạy Hub
  với tùy chọn backend phù hợp với bản Hub hiện tại.
- **Package resolve thất bại:** kiểm tra mạng, đóng Unity, mở lại; không xóa
  `Packages/manifest.json` hoặc tự ý nâng package.
- **Project mở rất chậm lần đầu:** chờ import hoàn tất; không commit `Library/`.
- **Scene load báo chưa đăng ký:** kiểm tra Build Profiles và bật đúng scene thay
  vì tạo scene rỗng cùng tên.

## Tạo Linux build

Trong Unity, mở **File > Build Profiles**, chọn/tạo Linux profile dựa trên profile
production, xác nhận scene list, chọn kiến trúc x86_64 rồi Build. Chạy executable
từ terminal để giữ log nếu cần điều tra lỗi runtime.
