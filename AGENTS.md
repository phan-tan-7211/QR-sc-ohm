# UC2836Live - hướng dẫn giao tiếp thiết bị

Trước khi sửa code, đọc `AI_CONTEXT.md`.

## UI/UX Rules

Trước khi sửa bất kỳ giao diện WPF nào, bắt buộc đọc:

1. `docs/design/DESIGN_SYSTEM.md`
2. `docs/design/UX_SPEC.md`
3. `docs/design/LAYOUT_SPEC.md`

Không thiết kế lại màn hình chỉ dựa trên prompt hiện tại hoặc ảnh tham khảo. Các quyết định đã duyệt trong bộ design spec của repo là nguồn sự thật cho UI.

Ứng dụng có đúng ba cửa sổ chính:

- `CommunicationWindow`: kiểm tra mức sẵn sàng của hệ thống.
- `MainWindow`: vận hành sản xuất, đo và xem PASS/FAIL.
- `DevSettingsWindow`: cấu hình và chẩn đoán kỹ thuật.

Nguyên tắc HMI công nghiệp (an toàn, rõ ràng, thao tác nhanh, trạng thái dễ đọc) được ưu tiên hơn trang trí kiểu dashboard. Không đưa logic USB CDC/SCPI, camera, QR hoặc session vào code-behind giao diện nếu service dùng chung đã tồn tại.

Sau khi sửa UI, kiểm tra tối thiểu build Debug và các kích thước `1366×768`, `1920×1080`, maximized. Khi có MCP UI khả dụng, dùng XamlMcp, WPFVisualTreeMcp và Netwright theo `docs/design/UX_SPEC.md`.

Thiết bị dùng USB CDC của Windows, xuất hiện dưới dạng COM3/COM4, giao thức SCPI ASCII 9600 8N1. Không được nhầm với Modbus RTU hoặc USB-TMC. Các lệnh chính đang dùng là `*IDN?`, `FUNC:IMP?`, `FREQ?`, `VOLT?`, `APER?`, `TRIG:SOUR?`, `TRIG`, và `FETC?`.
