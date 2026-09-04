# IngressLoadTest - Cross Platform

Target:

```text
.NET 10
net10.0
```

Project không dùng Windows Forms, Windows Service API hay API riêng của Windows.

## Mục tiêu

Cùng một source và cùng một portable publish có thể chạy trên:

```text
Windows
Ubuntu / Linux
```

bằng cùng lệnh:

```text
dotnet IngressLoadTest.dll
```

Điều này phù hợp để đặt LoadTest trên nhiều host khác nhau và so sánh:

```text
Windows load generator
Ubuntu load generator
different subnet / network path
different physical hosts
```

## Publish portable

Trên Windows:

```text
publish-portable.cmd
```

hoặc:

```bash
dotnet publish IngressLoadTest.csproj -c Release --self-contained false -o publish/portable
```

Kết quả trong:

```text
publish/portable/
```

Copy nguyên folder này sang Windows hoặc Ubuntu.

`UseAppHost=false` được bật trong `.csproj`, vì vậy không phụ thuộc file `.exe`
native của Windows. Entry point dùng chung là:

```bash
dotnet IngressLoadTest.dll
```

Máy chạy cần có .NET 10 Runtime.

## Windows

Trong folder publish:

```cmd
dotnet IngressLoadTest.dll
```

Chạy liên tục/restart sau khi process kết thúc:

```cmd
run_forever.cmd
```

## Ubuntu

Kiểm tra runtime:

```bash
dotnet --info
dotnet --list-runtimes
```

Trong folder publish:

```bash
chmod +x run_once.sh run_forever.sh
./run_once.sh
```

hoặc trực tiếp:

```bash
dotnet IngressLoadTest.dll
```

Chạy lại tự động nếu process kết thúc:

```bash
./run_forever.sh
```

Hai shell script cố gắng đặt:

```bash
ulimit -n 65535
```

để tránh giới hạn file descriptor quá thấp khi tạo nhiều TCP/HTTP connection.

Kiểm tra giới hạn hiện tại:

```bash
ulimit -n
```

## Thông tin môi trường

Khi startup, chương trình in và ghi log:

```text
OS
.NET Framework/runtime
Process Architecture
OS Architecture
CPU Count
Server GC
Stopwatch Frequency
BaseDirectory
```

Nhờ đó khi benchmark nhiều host có thể biết chính xác từng kết quả chạy trên môi
trường nào.

## Payload multi-client

Giữ nguyên cơ chế:

```text
payload.json + clients.txt
```

`clients.txt`:

```text
MXN|BKS|IMEI
1010|29Y56789_C|864281042291089
1010|29Y56790_C|864281042291090
...
```

Payload variant được tạo sẵn lúc startup.

## Dynamic Time

Trường:

```json
"Time": 1787991770
```

được cập nhật thành Unix timestamp hiện tại cho từng request:

```csharp
DateTimeOffset.UtcNow.ToUnixTimeSeconds()
```

Không parse/serialize lại toàn bộ JSON trong hot path.

## Benchmark network

Để so sánh đường truyền, nên giữ giống nhau trên mọi LoadTest host:

```text
appsettings.json
payload.json
clients.txt
TargetRps
WorkerCount
MaxConnectionsPerServer
RequestTimeoutSeconds
```

Chỉ thay:

```json
"Server": "<IngressHost IP>"
```

Ví dụ test nên ghi lại:

```text
Load generator OS
Load generator IP
IngressHost IP
Target RPS
Actual Success/s
Error/s
Avg/P95/P99 latency
CPU load generator
Network throughput
```

Nếu cùng một IngressHost nhưng LoadTest từ host A đạt thấp và host B đạt cao,
đó là tín hiệu mạnh để điều tra source host/network path thay vì IngressHost.
