# TODO

## Priority: refine UI sizing and match the approved design

- [ ] The redesigned UI is sometimes too large and does not yet match the approved mockup closely enough. Refine the window size, spacing, typography, buttons, and list-row heights while preserving the agreed visual style.
- [ ] Check the main window and dialogs at smaller window sizes, when maximized, and at common Windows display scaling levels. Keep controls proportionate and all text readable without clipping.

User feedback recorded on 2026-09-17. This is follow-up work; no UI changes were requested when adding this note.

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
