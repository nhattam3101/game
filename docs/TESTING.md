# Kiểm thử MonsterSurvival

Không đánh dấu PASS cho Play Mode nếu chưa thực sự chạy Unity Editor. Kiểm tra
source/YAML chỉ chứng minh cấu hình tĩnh và cú pháp, không thay thế runtime test.

## Kiểm tra tự động trong Editor

Chạy **Tools > MonsterSurvival > QA > Core Gameplay Audit** sau khi import và sau
khi thay đổi scene, prefab, wave hoặc player configuration. Console phải không có
compile error hoặc exception đỏ.

## Smoke test bắt buộc

### Main Menu và Player Hub

1. Mở MainMenu, bấm Play.
2. Tạo/chọn profile.
3. Mở Player Hub; xác nhận scene hiện ra và Console không báo scene chưa đăng ký.
4. Mở inventory/equipment/shop/achievement nếu có dữ liệu test.
5. Quay lại Main Menu; xác nhận input và button không bị nhân đôi.

### Gameplay

1. Bắt đầu run từ menu hoặc hub.
2. Kiểm tra WASD/mũi tên, dash Space và auto attack.
3. Nhận damage liên tục; xác nhận invulnerability ngắn hoạt động và health không
   giảm nhiều lần trong cùng một hit.
4. Nhặt XP, level-up và chọn skill; nếu tăng nhiều level cùng lúc, popup phải xử
   lý đủ lựa chọn theo hàng đợi.
5. Qua ít nhất hai wave; xác nhận enemy spawn ngoài vùng an toàn của player và độ
   khó tăng.
6. Pause/resume, game-over và quay lại Main Menu.

### Persistence

1. Ghi nhận profile, inventory/equipment và progression trước run.
2. Chuyển Menu → Hub → Gameplay → Menu.
3. Mở lại profile và xác nhận dữ liệu được giữ đúng, không nhân đôi reward/item.

## Kiểm tra trước commit

- Không có missing script trong scene/prefab đã sửa.
- Scene production đều Enabled trong Build Profile.
- Không có GUID trùng hoặc `.meta` bị mất.
- Không có file `Library`, `Temp`, log, backup hay build output trong diff.
- Không có thay đổi ngoài phạm vi trong scene YAML.

## Báo lỗi

Ghi rõ Unity version, hệ điều hành, scene bắt đầu, bước tái hiện, kết quả mong đợi,
kết quả thực tế, Console stack trace và ảnh/video nếu lỗi liên quan UI/VFX. Với
lỗi save, đính kèm cấu trúc dữ liệu đã ẩn thông tin cá nhân; không commit save cá
nhân vào repository.
