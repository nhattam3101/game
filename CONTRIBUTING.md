# Đóng góp vào MonsterSurvival

## Trước khi sửa

1. Mở dự án bằng Unity `6000.6.6f1`.
2. Pull branch mới nhất và bảo đảm working tree không chứa thay đổi lạ.
3. Chạy **Tools > MonsterSurvival > QA > Core Gameplay Audit**.
4. Tạo branch ngắn gọn, ví dụ `fix/player-hub-load` hoặc
   `feature/wave-modifier`.

## Đặt code và asset

- Code dùng chung đặt tại `Assets/MonsterSurvival/Code/Runtime/<Domain>`.
- Code chỉ chạy trong Editor đặt dưới một thư mục tên `Editor`.
- Hệ thống sở hữu cả code lẫn nhiều content riêng đặt tại
  `Assets/MonsterSurvival/Features/<Feature>`.
- Scene production đặt tại `Assets/MonsterSurvival/Content/Scenes`.
- Data dùng chung đặt tại `Content/Data`; prefab dùng chung đặt tại
  `Content/Prefabs`.
- Không tạo thư mục `Scripts`, `Misc`, `New Folder` hoặc `Temp` vì không thể hiện
  ownership.

Tên class, file C# và component dùng `PascalCase`. Field private dùng
`camelCase`; field serialize ưu tiên `[SerializeField] private` thay vì public.
Không đổi tên serialized field nếu chưa thêm `FormerlySerializedAs` hoặc có kế
hoạch migrate dữ liệu.

## Làm việc với Unity asset

- Di chuyển/đổi tên trong Unity Editor là lựa chọn an toàn nhất.
- Nếu di chuyển bằng file manager, phải di chuyển cả asset và `.meta` tương ứng.
- Không sửa GUID, không regenerate `.meta`, không commit `Library/`, `Temp/`,
  `Logs/`, `Obj/` hoặc file backup.
- Trước khi xóa asset, tìm cả đường dẫn và GUID trong repository.
- Hạn chế sửa scene/prefab dạng YAML bằng tay; luôn xem diff trước khi commit.

## Tiêu chuẩn code

- Component giữ một trách nhiệm rõ ràng; logic không phụ thuộc UI nên nằm ngoài
  UI component.
- Cache component reference thay vì gọi `Find*` trong `Update`.
- Dùng event cho thay đổi trạng thái quan trọng thay vì để UI polling mỗi frame.
- Giá trị cần tuning phải serialize và có range/min hợp lý.
- Scene transition phải có thông báo lỗi rõ ràng nếu scene chưa được đăng ký.
- Không thêm package mới nếu chưa nêu lý do và ảnh hưởng build size/license.

## Trước khi tạo pull request

1. Console không có compile error.
2. Core Gameplay Audit báo PASS.
3. Chạy checklist trong [docs/TESTING.md](docs/TESTING.md).
4. Kiểm tra `git diff` để chắc chắn không có GUID hoặc scene ngoài phạm vi bị đổi.
5. Mô tả scene/prefab đã sửa và bước test thủ công trong pull request.

Commit nên nhỏ và có mục đích, ví dụ `fix: register PlayerHub in build profile`.
