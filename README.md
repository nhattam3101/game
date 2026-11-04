# MonsterSurvival

MonsterSurvival là game survival 2D xây dựng bằng Unity `6000.6.6f1` (Unity 6.6).
Người chơi di chuyển, né đòn, tự động tấn công, chọn nâng cấp và phát triển nhân
vật qua các wave quái và boss.

## Bắt đầu nhanh

### Yêu cầu

- Git.
- Unity Hub.
- Unity Editor đúng phiên bản `6000.6.6f1` với module Linux Build Support nếu
  cần tạo bản build Linux. Profile hiện có tên `Windows`, vì vậy hãy tạo một
  profile Linux riêng thay vì đổi platform của profile production đang dùng.
- Khoảng trống đĩa đủ cho Unity import lại `Library/` trong lần mở đầu tiên.

Không commit hoặc sao chép thư mục `Library`, `Temp`, `Logs` và `Obj` giữa các
máy. Unity sẽ tự tạo lại chúng.

### Clone và mở dự án

```bash
git clone <repository-url> MonsterSurvival
cd MonsterSurvival
```

Trong Unity Hub, chọn **Add > Add project from disk**, trỏ đến thư mục chứa
`Assets`, `Packages` và `ProjectSettings`, sau đó mở bằng `6000.6.6f1`.

Lần import đầu có thể mất vài phút. Chờ thanh import hoàn tất và Console không
còn compile trước khi bấm Play. Không chấp nhận đề nghị nâng phiên bản Editor nếu
chỉ muốn chạy hoặc sửa dự án hiện tại.

### Chạy game trong Editor

1. Chạy **Tools > MonsterSurvival > QA > Core Gameplay Audit**.
2. Mở `Assets/MonsterSurvival/Content/Scenes/MainMenu.unity`.
3. Bấm Play, chọn profile rồi vào Player Hub hoặc bắt đầu gameplay.
4. Kiểm tra Console; lỗi màu đỏ phải được xử lý trước khi commit.

Build Profile production nằm tại
`Assets/MonsterSurvival/Settings/Build Profiles/Windows.asset` và phải
bật các scene Main Menu, Player Hub và Gameplay.

## Điều khiển

- Di chuyển: `WASD` hoặc phím mũi tên.
- Dash: `Space`.
- Pause / quay lại: `Escape`.
- Inventory: `I`.
- Tấn công: tự động.

## Cấu trúc dự án

```text
Assets/MonsterSurvival/
├── Art/                 # Sprite, animation, map art và UI art
├── Audio/               # Mixer, nhạc và sound effect
├── Code/
│   ├── Runtime/         # Code dùng chung, chia theo domain
│   └── Editor/          # Installer, audit và công cụ Editor
├── Content/             # Data, material, prefab và scene dùng chung
├── Features/            # Module có code/content thuộc riêng một feature
└── Settings/            # URP, template và Build Profiles
```

Code runtime dùng chung được chia thành `Core`, `Player`, `Combat`, `Enemies`,
`Waves`, `Skills`, `Bosses`, `World`, `UI`, `Audio` và `VFX`. Code thuộc riêng
một hệ thống lớn được giữ trong `Features/<FeatureName>` để tránh tạo thư mục
runtime dùng chung quá lớn.

## Tài liệu

- [Kiến trúc và luồng game](docs/ARCHITECTURE.md)
- [Cài đặt và chạy trên Fedora/Linux](docs/SETUP_LINUX.md)
- [Quy trình kiểm thử](docs/TESTING.md)
- [Quy tắc đóng góp](CONTRIBUTING.md)
- [Quy tắc tổ chức Assets](Assets/MonsterSurvival/README.md)

## Nguyên tắc an toàn

- Luôn di chuyển asset cùng file `.meta`; GUID là liên kết giữa scene, prefab và
  ScriptableObject.
- Không tạo bản sao backup bên trong `Assets`.
- Không sửa đồng thời cùng một scene lớn trên nhiều branch nếu chưa thống nhất.
- Không gọi scene bằng tên ngắn khi có thể dùng đường dẫn đầy đủ.
- Không xóa asset chỉ vì không thấy tham chiếu trong C#; prefab/scene có thể tham
  chiếu asset bằng GUID.
