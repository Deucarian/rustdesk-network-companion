# RustDeskHop — RustDesk Network Companion

RustDesk is excellent at connecting to devices, but it becomes awkward when one operator regularly uses more than one RustDesk network—for example, RustDesk's public network and a private self-hosted server.

This companion app provides a simple client-and-network launcher:

- Save named RustDesk network profiles.
- Associate each client with the network where its ID exists.
- Route each new connection through its assigned network, even when RustDesk's default is different.
- Keep existing RustDesk sessions open while connecting through another server.
- Confirm the route in a clear popup when it differs from RustDesk's default.
- Check private-server reachability before launching the connection.
- Detect an existing RustDesk account login and continue automatically.
- Guide the one-time public-server sign-in and resume the pending connection afterwards.

Normal connections use RustDesk's per-connection server syntax and do not rewrite its global configuration. The companion does not replace RustDesk, enter Google/Microsoft/GitHub credentials, or force-kill the RustDesk background service. Private profiles still require a working VPN or other route to the private server.

## The problem this solves

Without a network-aware launcher, users have to remember which server owns a RustDesk ID and manually change RustDesk's network settings. That can cause confusing “ID not found” errors and can interrupt existing sessions.

The intended workflow is:

1. Select a saved client.
2. The companion identifies the required network.
3. If it differs from the current RustDesk default, a confirmation explains that only the new connection is being routed differently.
4. Existing sessions remain connected.
5. The companion checks private-network reachability and opens the connection.

After the one-time RustDesk public-account setup, this is a single selection and confirmation. If public sign-in has never been completed, RustDeskHop can prepare the public profile with Windows administrator approval, open RustDesk, wait for the user to finish the browser login, and then continue the original connection automatically. Backups of any RustDesk configuration touched by this recovery flow are kept in a `RustDeskHop Backups` folder beside the original configuration.

## Configuration

On first run, use **Manage networks** and **Add client** to configure the profiles and IDs for your environment. Settings are stored in:

```text
%APPDATA%\SimultriaRustDeskCompanion\settings.json
```

The public repository intentionally contains only generic defaults. A developer-specific `settings.local.json` may be placed beside the project file; it is ignored by Git and copied to the build output for local development.

## Build

```powershell
dotnet build RustDeskCompanion.csproj -c Release
dotnet test tests\RustDeskHop.Tests\RustDeskHop.Tests.csproj -c Release
```

The executable is produced under `bin\Release\net9.0-windows\`.

## Branches and automation

- `develop` is the integration branch for ongoing work.
- `main` is the production branch.
- Pull requests targeting either branch run the Windows build check.
- Automated tests verify connection routing, RustDesk configuration detection, login detection, safe public-profile cleanup, and private-server probing.
- Every push to either branch produces a self-contained Windows ZIP artifact in GitHub Actions.

Because this is a desktop utility, the automated deployment target is a downloadable build artifact rather than a server. Versioned public releases can be added when the project is ready for a release process.

Feature ideas can be submitted through the repository's **Feature request** issue template.

## Downloads

The latest public release is always available from the stable link below:

<https://github.com/Deucarian/rustdesk-network-companion/releases/latest>

Release files use versioned names such as `RustDeskHop-v0.1.0-win-x64.exe` and `RustDeskHop-v0.1.0-win-x64.zip`. Stable releases are created automatically when a version tag such as `v0.1.0` is pushed; tags such as `v0.1.0-beta.1` become prereleases.

## License

This project is intended as an open-source companion around the RustDesk client. RustDesk itself remains under its own license and is not bundled here.
