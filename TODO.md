# TODO

## Priority: refine UI sizing and match the approved design

- [x] Refine the oversized UI while preserving the agreed visual style: smaller default windows, typography, buttons and spacing; text-sized computer rows; bounded dashboard/editor content when maximized; wrapping selected-computer details.
- [x] Check the main window and network manager at their minimum, default and maximized sizes at 100% Windows scaling; check the compact Add computer dialog. Fix input borders not repainting after resizing. Add regression coverage for layout containment, repeated resizing and long labels.
- [ ] Verify the main window and dialogs on real 125%, 150% and 200% Windows displays, including moving between monitors with different scaling. Keep all text readable and controls reachable; do not treat 100% screenshots as high-DPI validation.

User feedback recorded on 2026-09-17. Compact-layout implementation and 100% visual checks completed on 2026-09-20. Higher-DPI/mixed-monitor validation remains open.

## Next milestone: tray-first UX

- [ ] Add a system-tray mode so the companion can stay available without an open window.
- [ ] Add a tray menu with saved clients grouped by network profile.
- [ ] Add a polished network-switch confirmation popup.
- [ ] Show the current RustDesk network and the target network clearly in the popup.
- [ ] Show whether Tailscale/private-server access is ready before connecting.

## Client list and navigation

- [x] Replace the basic table with a cleaner, rounded computer list and selected-computer details.
- [ ] Add search/filtering to the computer list.
- [ ] Show client name, RustDesk ID, required network, and online/reachable status.
- [ ] Add last-connected time and recently used clients.
- [ ] Add client editing, not only add/remove.
- [ ] Add keyboard navigation and a quick-connect shortcut.
- [ ] Add configurable global hotkeys, for example `Ctrl+Shift+R`.

## Network profiles

- [x] Make Public and private profiles visually distinct with labeled badges and colors.
- [ ] Add a profile status/test button.
- [ ] Add an “Open Tailscale” action when a private profile is unavailable.
- [ ] Support importing and exporting profiles and client mappings.
- [ ] Keep server keys in local configuration and never commit machine-specific values.
- [ ] Remember the last selected profile without rewriting RustDesk’s global configuration.

## Session safety

- [ ] Detect visible RustDesk connection windows more accurately.
- [ ] Tell the user exactly which sessions will be closed before switching.
- [ ] Keep the RustDesk background service and unattended access untouched.
- [ ] Allow connecting without closing existing sessions when the user chooses.
- [ ] Add clear error messages for unreachable private networks, missing keys, and offline clients.

## Windows integration

- [x] Add a proper application icon.
- [ ] Add Start Menu and optional startup integration.
- [ ] Add a lightweight installer or packaged release.
- [ ] Consider code signing for release builds.
- [ ] Add a setting for minimizing to the tray.

## Quality and release

- [ ] Add unit tests for profile loading, target mapping, and connection-target construction.
- [ ] Add integration tests for private-server reachability checks.
- [x] Add GitHub Actions for build and test verification.
- [ ] Publish versioned release artifacts.
- [ ] Add screenshots and a short usage guide to the README.

## Future ideas

- [ ] Support multiple private RustDesk servers.
- [ ] Optionally remember a server profile per saved client automatically.
- [ ] Add connection history and lightweight diagnostics.
- [ ] Explore native RustDesk integration if a standalone launcher becomes limiting.
