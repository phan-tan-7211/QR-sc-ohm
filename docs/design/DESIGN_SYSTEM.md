# QR-SC-OHM Industrial Design System

Đây là luật giao diện chung của ứng dụng. Mọi thay đổi XAML hoặc style phải tuân theo tài liệu này.

## Thứ tự ưu tiên

1. An toàn và khả năng đọc từ xa.
2. Tốc độ thao tác của operator.
3. Tính nhất quán giữa ba cửa sổ.
4. Thẩm mỹ.

Không dùng màu trang trí để mô tả trạng thái thông thường. Màu phải có ý nghĩa ổn định:

- Bình thường: nền trung tính, chữ xanh đậm.
- Sẵn sàng: xanh lá/xanh ngọc nhẹ.
- Cảnh báo: vàng hoặc cam.
- Alarm, lỗi, FAIL: đỏ và có nhãn chữ rõ ràng.
- Disabled: xám, độ tương phản vẫn đủ để đọc.

## PASS/FAIL

- PASS và FAIL phải đọc được ở khoảng cách thao tác.
- FAIL có ưu tiên thị giác cao hơn trạng thái bình thường.
- Không chỉ dùng màu để phân biệt; luôn có chữ `PASS`, `FAIL`, `ERROR` hoặc trạng thái tương đương.
- Không đổi màu kỹ thuật của giá trị đo chỉ để tạo cảm giác đẹp.

## Khoảng cách và kích thước

- Chỉ dùng nhịp khoảng cách `4 / 8 / 12 / 16 / 24 / 32`.
- Nút thao tác operator tối thiểu cao `40px`; hành động sản xuất chính tối thiểu `44px`.
- Không tạo card chỉ để trang trí. Card phải chứa một trạng thái, phép đo hoặc hành động có ý nghĩa.
- Tránh `Width`/`Height` cố định khi nội dung có thể dài do dịch ngôn ngữ; ưu tiên `Grid` với `*`, `Auto`, `MinWidth` và `TextWrapping`.

## Typography

Thứ tự nhấn mạnh: giá trị đo > tiêu đề khu vực > nhãn > metadata phụ. QR value, COM port, giá trị đo, đơn vị kỹ thuật, SCPI và tên model giữ nguyên dữ liệu, không dịch.

## Ngôn ngữ

UI hỗ trợ `en-US` (mặc định/fallback), `vi-VN` và `ko-KR`. Chuỗi operator-facing phải đi qua `LocalizationManager`; key có tên semantic như `Common.Connect`, `Communication.EnterSystem`, `Main.Result`, `Settings.Title`. Thiếu bản dịch phải fallback English, không để trống.

## Không thay đổi tùy tiện

Không tự ý đổi màu trạng thái, cỡ chữ giá trị đo, ngưỡng PASS/FAIL, thứ tự cột lịch sử, giao thức USB CDC/SCPI hoặc hành vi camera/QR khi chỉ sửa layout.
