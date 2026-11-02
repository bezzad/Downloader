## 1. Decide

- [x] 1.1 Owner: use the system proxy when `RequestConfiguration.Proxy` is null? (yes / no) — owner: yes.

## 2. File name without folder

- [x] 2.1 Test (fails now): builder with only `WithUrl` + `WithFileName` downloads to that name.
- [x] 2.2 Fix in `Download.StartAsync`.

## 3. Builder exception

- [x] 3.1 Test: `Build()` without URL throws with a clear message.
- [x] 3.2 Fix the exception.

## 4. FileExistException message

- [x] 4.1 Test: the message contains the file path.
- [x] 4.2 Add the message.

## 5. Throttle and cancel

- [x] 5.1 Test (fails now): with `MaximumBytesPerSecond = 1024` and `BufferBlockSize = 1024 * 64`, cancel ends the download within a short time.
- [x] 5.2 Pass the token from `ReadAsync` to `Throttle` / `Task.Delay`.

## 6. System proxy (only if 1.1 = yes)

- [x] 6.1 Test with `HttpClient.DefaultProxy` pointing at a small test proxy (`SystemProxyTest`, `FakeProxyServer`).
- [x] 6.2 Change `UseProxy` / `Proxy` setup; update the `Proxy` XML doc and README.

## 7. Finish

- [x] 7.1 CHANGELOG entries.
- [x] 7.2 Build with 0 warnings; all tests green.
