# IngressLoadTest - Multi Client + Dynamic Time

Môi trường:

- Visual Studio 2026
- .NET 10

## Dữ liệu request

`payload.json` là template:

```json
{
  "BKS": "29Y56789_C",
  "IMEI": "864281042291089",
  "MXN": "1010",
  "Acquy": 19011,
  "Time": 1787991770
}
```

`clients.txt` cung cấp các giá trị thay đổi theo client:

```text
MXN|BKS|IMEI
1010|29Y56789_C|864281042291089
1010|29Y56790_C|864281042291090
1011|30A12345|864281042291092
```

## Trường Time

`Time` là Unix timestamp theo giây.

Khi gửi từng HTTP request, chương trình cập nhật:

```csharp
DateTimeOffset.UtcNow.ToUnixTimeSeconds()
```

Ví dụ:

```json
"Time": 1787991770
```

sẽ được thay bằng Unix timestamp của hệ thống tại thời điểm request được gửi.

## Tối ưu hiệu năng

Không `JsonSerializer.Serialize()` lại toàn bộ JSON cho từng request.

Khi startup:

```text
payload.json
     +
clients.txt
     ↓
tạo PreparedPayload[] một lần
     ↓
mỗi PreparedPayload được tách thành:
Prefix + Time + Suffix
```

Ví dụ:

```text
Prefix:
{"BKS":"29Y56789_C","IMEI":"...","MXN":"1010","Acquy":19011,"Time":

Suffix:
}
```

Khi request được gửi:

```text
1. chọn client theo round-robin
2. lấy Unix timestamp hiện tại
3. ghi Prefix vào buffer worker
4. ghi Time trực tiếp dạng UTF-8 số
5. ghi Suffix
6. POST buffer
```

Mỗi worker có một `byte[]` buffer riêng và tái sử dụng buffer đó cho các request
tuần tự của worker.

Hot path KHÔNG thực hiện:

```text
Json parse
Json serialize
đọc file
string.Replace
new byte[] cho payload mỗi request
```

Điều này giúp việc cập nhật `Time` ảnh hưởng rất ít tới benchmark 20k+ RPS.

## Database test

Nếu `clients.txt` có 10,000 IMEI:

```text
Redis:
~10,000 keys CameraDevice:<IMEI>

MongoDB:
~10,000 latest-state documents
```

Các request tiếp tục round-robin qua các client.

Vì `Time` được cập nhật mỗi request nên dữ liệu Mongo/Redis của từng IMEI cũng
thể hiện thời điểm giả lập mới nhất thay vì giữ nguyên timestamp của template.
