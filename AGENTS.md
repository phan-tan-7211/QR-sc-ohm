# UC2836Live - hướng dẫn giao tiếp thiết bị

Trước khi sửa code, đọc `AI_CONTEXT.md`.

Thiết bị dùng USB CDC của Windows, xuất hiện dưới dạng COM3/COM4, giao thức SCPI ASCII 9600 8N1. Không được nhầm với Modbus RTU hoặc USB-TMC. Các lệnh chính đang dùng là `*IDN?`, `FUNC:IMP?`, `FREQ?`, `VOLT?`, `APER?`, `TRIG:SOUR?`, `TRIG`, và `FETC?`.
