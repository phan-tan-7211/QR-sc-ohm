# UC2836CX+ Live View

## UI/UX design memory

Các quy tắc giao diện lâu dài nằm trong [AGENTS.md](AGENTS.md) và [docs/design](docs/design/). Đọc `DESIGN_SYSTEM.md`, `UX_SPEC.md` và `LAYOUT_SPEC.md` trước khi sửa WPF UI. Các repo bên ngoài chỉ được ghi nhận trong `REFERENCES.md`, không được copy vào source.

Ứng dụng C# / WPF cho Windows, giao tiếp **USB CDC qua cổng COM ảo bằng SCPI ASCII**. Không dùng Modbus, USB-TMC, GPIB hoặc HID. Xem [AI_CONTEXT.md](AI_CONTEXT.md) trước khi sửa giao tiếp thiết bị.

## Chạy

Nhấp đúp `Chay UC2836 Live.bat` ở thư mục nhà cung cấp. Ứng dụng tự kết nối COM3, 9600 baud. Có thể chạy trực tiếp `bin/Release/net10.0-windows/UC2836Live.exe`, chọn cổng rồi bấm **Bắt đầu live**.

Đóng phần mềm khác đang chiếm COM3 trước khi kết nối. Bấm **Dừng** để đóng cổng. Khi thay đổi chế độ đo trên mặt máy, dừng rồi bắt đầu lại để đọc cấu hình mới.

## Chức năng

- Hiển thị LA, LB, C, điện trở DC nếu bật, cực tính, mã so sánh, kiểm tra đấu dây.
- Ô **Mã QR sản phẩm** nhận dữ liệu từ máy quét QR USB giả lập bàn phím. Đặt con trỏ vào ô này, quét mã có hậu tố Enter, sau đó thực hiện phép đo; QR, giá trị điện trở/điện cảm và PASS/FAIL được lưu cùng một dòng CSV.
- Hỗ trợ định dạng một/hai tần số, có/không DCR.
- Biểu đồ 150 phản hồi gần nhất; bảng giữ 2.000 phản hồi.
- Toàn bộ phiên tự lưu vào thư mục `Sessions` cạnh chương trình. Nút **Xuất CSV** tạo bản sao dữ liệu tới thời điểm bấm.
- Mặc định giữ nguyên trigger do máy cài đặt và đọc kết quả bằng `FETC?`. Có tùy chọn chỉ ghi kết quả khi có QR; app chưa giả lập khóa Auto Trig Z vì tài liệu hãng chưa công bố lệnh điều khiển chức năng này.

## Giới hạn được thể hiện rõ

`FETC?` trả kết quả gần nhất, không có số thứ tự phép đo nên các phản hồi có thể trùng phép đo. Tốc độ hiển thị phản hồi không phải tốc độ phép đo độc lập. Khi máy ở trigger ngoài hoặc BUS, cần nguồn kích hoạt thích hợp; ứng dụng này không tự chuyển chế độ.

Giao diện tự chọn tiền tố SI (H/mH/µH, Ω/mΩ/kΩ, F/µF/nF/pF...) từ giá trị và phép đo đọc bằng FUNC:IMP?. Đây là quy đổi hiển thị, không thay đổi dải đo phần cứng. Bảng và các ô đo có đơn vị; CSV giữ giá trị gốc để phân tích. Khi đổi phép đo trên mặt máy, ngắt và kết nối lại để cập nhật nhãn/đơn vị.

Chế độ LCR hỗ trợ phản hồi 3 hoặc 4 trường; Ls–DCR hiển thị điện cảm và điện trở DC. Khi máy chưa trả dữ liệu ở Single/trigger ngoài, ứng dụng giữ kết nối và báo chờ DUT. BIN 1–9 với trạng thái 0 được hiển thị PASS; BIN 0 hiển thị OUT/chưa so sánh. Chế độ cân bằng vẫn giữ nguyên mã so sánh vì không suy diễn từ bảng mã LCR.

Phiên bản đầu tập trung xem trực tiếp và thu dữ liệu; chưa có giao diện ghi cấu hình. Không gửi Modbus lên kết nối SCPI.

CSV chứa thời gian máy tính nhận dữ liệu, mã QR, PASS/FAIL, các trường chính, chế độ và chuỗi phản hồi gốc (bao gồm toàn bộ dữ liệu hai tần số). Bản hiện tại chưa dùng webcam; camera sẽ cần thêm thư viện giải mã QR riêng.

## Build / kiểm tra

Yêu cầu .NET 10 SDK; máy chạy cần .NET 10 Desktop Runtime.

```powershell
dotnet build UC2836Live.csproj -c Release
.\bin\Release\net10.0-windows\UC2836Live.exe --self-test
.\bin\Release\net10.0-windows\UC2836Live.exe --probe
```

`--self-test` kiểm tra parser; `--probe` đọc 15 phản hồi thật qua COM3 và đóng cổng. Kết quả ghi tại `verification.txt` cạnh exe.
