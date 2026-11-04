# MonsterSurvival asset layout

Mọi production asset của game nằm dưới root này. TextMesh Pro và package/vendor
asset giữ ở thư mục riêng để dễ cập nhật và phân biệt ownership.

## Thư mục cấp cao

- `Art/`: sprite, animation, map art, boss art và UI art.
- `Audio/`: mixer, music và sound effect asset.
- `Code/Runtime/`: component/runtime service dùng chung giữa nhiều feature.
- `Code/Editor/`: installer, audit và công cụ chỉ chạy trong Unity Editor.
- `Content/Data/`: ScriptableObject/data dùng chung.
- `Content/Materials/`: material dùng chung.
- `Content/Prefabs/`: prefab dùng chung.
- `Content/Scenes/`: scene production được đăng ký trong Build Profiles.
- `Features/`: feature sở hữu code và content theo gameplay domain.
- `Settings/`: render pipeline, template và Build Profiles.

## Code/Runtime

- `Core/`: session state, health và primitive nền tảng.
- `Player/`: input, movement, stats, progression, shield và shooting.
- `Combat/`: projectile, impact và combat effect data.
- `Enemies/`: enemy AI chung và archetype enemy.
- `Waves/`: spawn, wave progression và elite scheduling.
- `Skills/`: dữ liệu và lựa chọn skill trong run.
- `Bosses/`: boss behaviour dùng chung.
- `World/`: map/pathfinding/obstacle runtime.
- `UI/`: menu, HUD, popup và presentation component.
- `Audio/`: audio orchestration và event bridge.
- `VFX/`: component điều khiển hiệu ứng hình ảnh.

Không đặt file C# trực tiếp ở gốc `Runtime`; chọn domain có ownership gần nhất.
Nếu code chỉ phục vụ một feature có content riêng, đặt cạnh feature đó thay vì
đẩy vào shared runtime.

## Quy tắc

1. Di chuyển Unity asset cùng file `.meta` để GUID không đổi.
2. Code editor-only luôn nằm dưới một thư mục tên `Editor`.
3. Backup, QA export và generated report phải ở ngoài `Assets`; các đường dẫn này
   đã được Git ignore.
4. Runtime scene loading và Build Profile tooling dùng đường dẫn scene đầy đủ.
5. Không thêm scene lịch sử, package example hoặc file sinh tự động vào `Assets`.
6. Trước khi xóa asset, tìm cả đường dẫn lẫn GUID vì scene/prefab có thể tham
   chiếu mà C# không thể hiện.

Xem kiến trúc chi tiết tại `docs/ARCHITECTURE.md` ở root repository.
