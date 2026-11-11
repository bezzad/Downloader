## Why

From the review of 7.0.1 (2026-11-11): issues **H2, H3, M10, M11, L7, L9**.

- **H2 (High). Resume from a package can give a broken file that is reported as Completed.**
  `DownloadFileTaskAsync(package)` trusts the chunk positions in the package and checks neither the temp
  file nor the server:
  - If the `.download` file was deleted or replaced, a new empty file is made and the download goes on
    from the old positions. The first part of the file is zeros.
  - If the file on the server changed (new size, `ETag` or `Last-Modified`), the new values are written
    over the old ones without a compare. The result mixes old and new bytes.
  The `safer-resume` change added these checks only to `TryResumeFromExistingFile`.
- **H3 (High). Two downloads to the same file at the same time lose the file.** Both write to the same
  `name.download`. `FileExistPolicy.Rename` looks only at the final name. On Linux both end as
  `Completed`, the second one deletes the finished file of the first, its own rename fails, and no file
  is left. This happens when a user adds the same link twice.
- **M10 (Medium).** `FileExistPolicy.Delete` deletes the old file when the download STARTS. If the new
  download fails, the old file is lost. The replace at the end is two steps, not one.
- **M11 (Medium).** A file name from the server is trusted too much: with the default `Delete` policy the
  server can replace any file in the folder by choosing its name. Windows device names (`CON`, `NUL`,
  `COM1`) and names over 255 bytes are not handled.
- **L7 (Low).** A stale `.download` file with a long tail (up to 2 GB) is read into memory before it is
  rejected.
- **L9 (Low).** Resume takes `Timeout` and `MaxTryAgainOnFailure` from the saved file, not from the
  current settings. The saved chunk layout is not checked for gaps or overlaps.

## What Changes

- H2: when a package is resumed, compare the new probe (size, `ETag`, `Last-Modified`) with the package,
  and check the temp file. If one changed, or the temp file is missing or shorter than the file size,
  clear the chunks and start from zero.
- H3: open the temp file with `FileShare.Read`, so a second writer fails at once with a clear error.
- M10: do not delete at the start; replace at the end with `File.Move(source, target, overwrite: true)`.
- M11: for a name the server chose, use `Rename` unless the caller set a policy; block device names and
  cut long names.
- L7: reject a metadata tail above a small cap (16 MB).
- L9: set `Timeout` and `MaxTryAgainOnFailure` from the configuration after loading; check that the
  chunks cover the file exactly.

## Capabilities

### New Capabilities
- `file-safety`: the downloaded file is right, or the download says it is not.

## Impact

- `DownloadService.cs`, `DownloadPackage.cs`, `ConcurrentStream.cs`, `Extensions/FileHelper.cs`,
  `Chunk.cs`.
- M10 and M11 change default behavior: next major version, or an option.
