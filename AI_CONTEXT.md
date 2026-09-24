# Ghi nhớ bắt buộc cho AI - UC2836CX+

## Chuẩn kết nối thực tế

- Thiết bị UC2836CX+ trong dự án này giao tiếp bằng **USB CDC**.
- Windows hiển thị USB CDC thành **cổng COM ảo** (ví dụ COM3 hoặc COM4).
- Thông số hiện dùng: **9600 baud, 8 data bits, No parity, 1 stop bit (8N1)**.
- Giao thức trên cổng COM là **SCPI dạng ASCII**, mỗi lệnh kết thúc bằng LF.
- Không được tự đổi sang Modbus RTU, USB-TMC, GPIB, HID hoặc giao thức USB khác.
- Tài liệu Modbus trong thư mục manual chỉ dùng tham khảo khi người dùng yêu cầu Modbus; app hiện tại không dùng Modbus.

## Nhận diện và lệnh SCPI đã xác nhận

- `*IDN?` nhận diện thiết bị, chuỗi có `UC2836CX+`.
- `FUNC:IMP?` đọc chức năng đo hiện tại (LCR/Ls-DCR hoặc balance).
- `FREQ?`, `VOLT?`, `APER?`, `TRIG:SOUR?` đọc cấu hình đo.
- `FETC?` đọc kết quả gần nhất.
- `TRIG:SOUR BUS` chọn kích đo từ cổng COM.
- `TRIG` kích một phép đo khi trigger source là BUS.
- Sau khi kích dùng `FETC?` để đọc kết quả.
- `DISP:PAGE?`, `:TRAN:MODE?`, `:TRAN:DF?`, `:TRAN:DCR?` đọc trang hiển thị và chế độ balance/DCR.

## Quy tắc sửa app

- Luôn dùng `System.IO.Ports.SerialPort` với USB CDC/COM và SCPI ASCII.
- Không gửi thanh ghi Modbus lên cổng SCPI.
- Không tự thay đổi tần số, điện áp, tốc độ hoặc trigger của máy nếu người dùng chưa bật chức năng tương ứng.
- Không dùng `TRIG:SOUR HOLD` làm khóa Auto Trig Z; thực tế máy vẫn có thể tự kích khi Auto Trig Z bật. App hiện giữ nguyên trigger của máy.
- Nếu chỉ cần đọc dữ liệu mà không muốn kích máy, dùng `FETC?` theo đúng trạng thái trigger hiện tại.
- CSV phải giữ chuỗi phản hồi gốc để truy vết.

## Khả năng phần cứng

- USB CDC/SCPI đọc được cấu hình, kết quả LCR/Ls-DCR, balance, DCR, mã BIN/trạng thái và trigger BUS.
- Muốn biết chính xác DUT đã đặt vào gá cần tín hiệu HANDLER (`EXT.TRIG`, `/INDEX`, `/EOM`) hoặc cảm biến ngoài; USB CDC không tự đọc được trạng thái cơ khí đó.
- Chế độ BUS chỉ là phương án dự phòng, vì QR sẽ trở thành lệnh kích và làm mất DUT auto-trigger.
- Thực tế đã kiểm tra: `TRIG:SOUR HOLD/CLEAR/PULSE/INT/MAN/EXT` không khóa được Auto Trig Z khi Auto Trig Z đang bật; Auto Trig Z là cơ chế riêng của máy.
- Tài liệu SCPI hiện không công bố lệnh bật/tắt Auto Trig Z. Bảng Modbus cũng không có thanh ghi Auto Trig Z; không được tự đoán tên lệnh.

## Yêu cầu vận hành đã chốt

- QR là **điều kiện cho phép đo**, không phải lệnh kích đo.
- Sau khi QR hợp lệ, DUT vẫn phải được máy tự nhận biết, chờ thời gian ổn định X giây theo cài đặt của máy, rồi máy tự trigger.
- Không được đổi sang BUS trigger chỉ để có QR nếu việc đó làm mất cơ chế tự nhận biết DUT.
- Nếu không có QR thì phải chặn phép đo trước tầng trigger. Chỉ đọc rồi bỏ kết quả sau khi máy đã đo là chưa đạt yêu cầu.
- USB CDC/SCPI không tạo được tín hiệu điện AND với cảm biến DUT. Muốn giữ DUT auto-trigger mà vẫn khóa theo QR cần interlock phần cứng/PLC/relay kết hợp tín hiệu jig hoặc HANDLER/EXT.TRIG. Nếu không có phần cứng này, phải chấp nhận BUS trigger hoặc chỉ loại bỏ kết quả sau đo.

## Tài liệu nguồn

- `UC2836CX+\\manual\\UC2835-36系列SCPI指令集.pdf`
- `UC2836CX+\\manual\\UC2836CX User Manual.pdf`
- `UC2836CX+\\manual\\UC2835+系列Modbus通讯协议.pdf` (giao thức khác, không dùng trong app hiện tại)
