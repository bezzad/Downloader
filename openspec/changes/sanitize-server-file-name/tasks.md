## 1. Tests first (must fail on the current code)

- [ ] 1.1 Add a dummy-server endpoint that answers with a given `Content-Disposition` value (and a `DummyFileHelper` URL builder).
- [ ] 1.2 Add tests: `../evil.txt`, `..\..\evil.txt`, `/etc/evil`, `C:\evil.txt` all end up inside the target folder with the name `evil.txt`.
- [ ] 1.3 Add tests: `FileName="a.zip"` gives `a.zip`; `filename*=UTF-8''%E2%82%AC.txt` gives `€.txt`; `a:b?.txt` gives a valid name; an empty or `..` name falls back to the URL name.
- [ ] 1.4 Run the new tests and confirm they fail.

## 2. Fix

- [ ] 2.1 Add the name-cleaning helper (one static method, no new public API).
- [ ] 2.2 Parse `filename*=` before `filename=`, case-insensitive, and remove the case-sensitive `Replace`.
- [ ] 2.3 Call the helper in `SocketClient.SetRequestFileNameAsync` before the name is stored on the request.
- [ ] 2.4 In `DownloadFileTaskAsync(urls, folder)`, check that `Path.GetFullPath(result)` starts with the folder path; else throw.

## 3. Finish

- [ ] 3.1 README "Step 4b": say the server name is cleaned.
- [ ] 3.2 CHANGELOG entry (security fix).
- [ ] 3.3 Build with 0 warnings; run all tests (net10.0 and net11.0) green.
