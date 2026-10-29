## 1. Decide

- [ ] 1.1 Owner: use the system proxy when `RequestConfiguration.Proxy` is null? (yes / no)

## 2. File name without folder

- [ ] 2.1 Test (fails now): builder with only `WithUrl` + `WithFileName` downloads to that name.
- [ ] 2.2 Fix in `Download.StartAsync`.

## 3. Builder exception

- [ ] 3.1 Test: `Build()` without URL throws with a clear message.
- [ ] 3.2 Fix the exception.

## 4. FileExistException message

- [ ] 4.1 Test: the message contains the file path.
- [ ] 4.2 Add the message.

## 5. Throttle and cancel

- [ ] 5.1 Test (fails now): with `MaximumBytesPerSecond = 1024` and `BufferBlockSize = 1024 * 64`, cancel ends the download within a short time.
- [ ] 5.2 Pass the token from `ReadAsync` to `Throttle` / `Task.Delay`.

## 6. System proxy (only if 1.1 = yes)

- [ ] 6.1 Test with `HttpClient.DefaultProxy` pointing at a small test proxy.
- [ ] 6.2 Change `UseProxy` / `Proxy` setup; update the `Proxy` XML doc and README.

## 7. Finish

- [ ] 7.1 CHANGELOG entries.
- [ ] 7.2 Build with 0 warnings; all tests green.
