# QR-SC-OHM UX Specification

## Ba cửa sổ chính

### CommunicationWindow

Mục đích: trả lời câu hỏi “hệ thống đã sẵn sàng để vận hành chưa?”. Đây là cửa sổ đầu tiên.

Luồng chuẩn:

`START → kiểm tra UC2836 → chọn camera → thấy live preview → QR decoder ready → storage ready → ENTER SYSTEM → MainWindow`

Communication phải hiển thị trạng thái rõ ràng cho UC2836, camera, QR decoder và storage. Live preview là bắt buộc; trạng thái Connected một mình chưa đủ. COM `Test` chỉ đọc nhận diện, không thay đổi cấu hình đo.

`ENTER SYSTEM` chỉ bật khi các thành phần bắt buộc sẵn sàng. Khi lỗi, hiển thị nguyên nhân có thể thao tác lại (`Retry`, `Reconnect`) và không crash UI thread.

### MainWindow

Mục đích: operator biết sản phẩm nào đang đo, giá trị chính/phụ, trạng thái phép đo và PASS/FAIL.

- Nhận cùng instance `CommunicationManager`, `CameraService`, `QrDecoderService`, `SessionService` từ CommunicationWindow.
- Không mở COM hoặc camera lần thứ hai khi chuyển cửa sổ.
- Giữ nguyên protocol UC2836 USB CDC/SCPI, parser, trigger, QR pipeline và CSV session.
- QR value và dữ liệu kỹ thuật không dịch.
- Khi thiếu QR hoặc QR hết hạn, hiển thị trạng thái rõ ràng theo cài đặt; không giả mạo PASS.

### DevSettingsWindow

Mục đích: cấu hình và chẩn đoán kỹ thuật. `Apply` lưu thay đổi hợp lệ; `Cancel` bỏ thay đổi chưa áp dụng. Chuỗi trợ giúp và lỗi phải được localization. Không dịch giá trị QR, COM, đơn vị hoặc thông số kỹ thuật.

## Ngôn ngữ runtime

Đổi English → Tiếng Việt → 한국어 phải cập nhật các cửa sổ đang mở, không reconnect UC2836, không restart camera, không xóa QR và không xóa lịch sử đo. Ngôn ngữ được lưu và khôi phục khi mở app lần sau.

## Failure paths bắt buộc

Phải xử lý an toàn: không có UC2836, sai COM, rút USB, không có camera, nhiều camera, camera không có frame, camera mất giữa session, QR không đọc được/trùng, CSV không ghi được, đóng CommunicationWindow và chuyển Communication → Main.

## Quy trình nghiệm thu UI

`CODE → BUILD DEBUG → RUN → XamlMcp screenshot/runtime → WPFVisualTreeMcp tree/layout → Netwright workflow → FIX → TEST LẠI`.

Nghiệm thu cả đổi ngôn ngữ, chọn COM, connect, chọn camera, QR test, ENTER SYSTEM và resize.
