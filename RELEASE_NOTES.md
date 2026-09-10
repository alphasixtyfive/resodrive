ResoDrive 0.3.6 makes caching explicit and consistent when adding or editing a drive.

- Choose read and write caching, writes only, minimal caching, or no disk cache.
- Set a cache size target and how long unused cached files are kept, using presets or custom values.
- Review the configured performance options and get useful validation errors before saving.
- New drives explicitly enable read and write caching. Existing drives retain their settings, including legacy defaults, until you change them.

Existing users: open Edit drive > Advanced to review caching. An older drive may now correctly show **Writes only** where the previous editor showed **Standard**. Select **Read and write cache (recommended)** to enable caching of downloaded data, choose the size and retention appropriate for your deployment, then save and accept **Apply and reconnect** if prompted.

The normal download is `ResoDrive-Setup.exe`. This update preserves your settings and credentials. Deployment profiles affect new connections; updating a profile does not rewrite existing drives.
