# IngressLoadTest

Phiên bản cập nhật để chạy giả lập dài hạn.

## Môi trường

- Visual Studio 2026
- .NET 10

## Xử lý lỗi

Chương trình có các lớp bảo vệ:

1. `try/catch` trên từng HTTP request.
2. `try/catch` trên producer.
3. `try/catch` trên worker.
4. `try/catch` trên monitor.
5. `try/catch` top-level trong `Program.Main`.
6. `AppDomain.CurrentDomain.UnhandledException`.
7. `TaskScheduler.UnobservedTaskException`.
8. `AppDomain.CurrentDomain.ProcessExit`.

Các exception managed bắt được sẽ ghi vào:

```text
log.txt
```

cùng folder với EXE.

## run_forever.cmd

Nếu process bị terminate ở mức mà chính process không thể ghi log hoặc tự phục hồi,
hãy chạy bằng:

```text
run_forever.cmd
```

Watchdog bên ngoài sẽ:

```text
start IngressLoadTest.exe
-> chờ process kết thúc
-> ghi ExitCode vào log.txt
-> đợi 5 giây
-> start lại
```

Điều này hữu ích với các lỗi kiểu process terminate/fatal/native.

## Quan trọng

`UnhandledException` có thể giúp ghi log, nhưng khi `IsTerminating=true`
thì .NET vẫn sẽ terminate process. Handler không thể "nuốt" lỗi đó để tiếp tục.

Các lỗi như sau cũng không thể đảm bảo bắt được từ chính process:

- `Environment.FailFast`
- `StackOverflowException`
- native crash / access violation nghiêm trọng
- process bị Task Manager kill
- máy bị reboot / mất nguồn
- OOM quá nghiêm trọng khiến không còn tài nguyên để ghi log

Trong các trường hợp đó, `run_forever.cmd` là lớp bảo vệ bên ngoài.

## Bộ nhớ

Phiên bản này không giữ từng latency sample vô hạn.

P50/P95/P99 dùng fixed histogram, do đó bộ nhớ thống kê gần như cố định
khi chạy nhiều giờ/ngày.
