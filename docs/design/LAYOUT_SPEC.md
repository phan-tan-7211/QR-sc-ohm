# QR-SC-OHM Layout Specification

## Breakpoints bắt buộc

- `1366×768`: bố cục không clipping, không có horizontal scroll; các nút chính vẫn thao tác được.
- `1920×1080`: tận dụng khoảng trống bằng `*`, không phóng giá trị đo đến mức mất cân đối.
- Maximized: nội dung co giãn theo cửa sổ; camera, biểu đồ và bảng lịch sử không che nhau.
- Restore từ maximized phải trả về kích thước hợp lệ.

## CommunicationWindow

Header: tên ứng dụng và mục đích Communication.

Thân cửa sổ chia hai vùng ngang:

- Trái: UC2836, COM selector, `Test`, `Reconnect`, trạng thái kết nối.
- Phải: camera selector, live preview luôn nhìn thấy, trạng thái streaming, QR decoder và mã QR cuối.

Footer giữa: storage state, tổng readiness, language selector và `ENTER SYSTEM`. Preview không được thay bằng placeholder khi camera đang streaming mà chưa có frame; trường hợp đó phải là trạng thái lỗi/chờ frame rõ ràng.

## MainWindow

Header: model/trạm đo, trạng thái hệ thống và language selector.

Thanh thao tác: port, baud, tìm cổng, connect/disconnect, export CSV, chu kỳ đọc.

Khu vực đo: giá trị chính, giá trị phụ, trạng thái phép đo/PASS/FAIL.

Khu vực camera: preview và QR state; camera không làm mất diện tích cần thiết cho giá trị đo.

Biểu đồ: nằm dưới khu vực đo, co giãn theo chiều rộng.

Bảng lịch sử: nằm cuối, có thể cuộn theo chiều dọc; cột kết quả phải dễ tìm và không bị đẩy ra ngoài chiều rộng nhỏ.

## DevSettingsWindow

Form một cột nội dung với label/help bên trái, input bên phải, lỗi gần vùng form và hai nút `Cancel`/`Apply` ở cuối. Label dài khi dịch phải wrap thay vì cắt chữ.

## Quy tắc co giãn

Ưu tiên `Grid` row/column `*` và `Auto`; tránh kích thước tuyệt đối cho text user-facing. Mọi chuỗi dịch dài hơn English phải được kiểm tra ở cả ba breakpoint.
