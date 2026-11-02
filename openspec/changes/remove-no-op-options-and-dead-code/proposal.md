## Why

**Priority: Low (clean-up).**

- `RequestConfiguration.ImpersonationLevel`, `Pipelined` and `UseDefaultCredentials` are never read.
  Users set them and nothing happens. (Their XML docs now say "no effect"; `CachePolicy`,
  `ConnectionGroupName` and `SendChunked` are already `[Obsolete]` for the same reason.)
- `ExceptionHelper.HasTypeOf` and `ExceptionHelper.HasSource` are not used anywhere. `HasSource` is the
  `Exception.Source` pattern that issue #226 says must never be used (empty under AOT), so keeping it
  invites a regression.
- `DownloadConfiguration.DownloadFileExtension` setter: `'.' + value.Trim('.').Trim(',').Trim(' ')
  .ToLower().Replace(" ", "").ToLower()` calls `ToLower` twice and trims in an odd order.

## What Changes

- Mark the three properties `[Obsolete("This property is obsolete and has no effect.")]` (do not remove
  them; that would break callers).
- Delete `HasTypeOf` and `HasSource` (internal, no public break).
- Simplify the extension setter to one clear expression with the same result for normal input.

## Capabilities

### New Capabilities
- `api-hygiene`: options that do nothing are marked.

## Impact

- `RequestConfiguration.cs`, `Extensions/ExceptionHelper.cs`, `DownloadConfiguration.cs`, and tests
  that set these properties (they will need `#pragma warning disable CS0618` or removal, to keep
  0 warnings).
