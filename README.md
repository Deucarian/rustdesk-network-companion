# RustDesk Network Companion

RustDesk is excellent at connecting to devices, but it becomes awkward when one operator regularly uses more than one RustDesk network—for example, RustDesk's public network and a private self-hosted server.

This companion app provides a simple client-and-network launcher:

- Save named RustDesk network profiles.
- Associate each client with the network where its ID exists.
- Warn before connecting through a different network.
- Optionally close visible RustDesk windows before switching.
- Check private-server reachability before launching the connection.
- Start RustDesk with its per-connection server syntax, without rewriting the global RustDesk configuration.

The companion does not replace RustDesk, log users into Tailscale, or force-kill the RustDesk background service. Private profiles still require a working VPN or other route to the private server.

## The problem this solves

Without a network-aware launcher, users have to remember which server owns a RustDesk ID and manually change RustDesk's network settings. That can cause confusing “ID not found” errors and can interrupt existing sessions.

The intended workflow is:

1. Select a saved client.
2. The companion identifies the required network.
3. If it differs from the current RustDesk default, a confirmation appears.
4. After confirmation, the companion checks reachability and opens the connection.

## Configuration

On first run, use **Manage networks** and **Add client** to configure the profiles and IDs for your environment. Settings are stored in:

```text
%APPDATA%\SimultriaRustDeskCompanion\settings.json
```

The public repository intentionally contains only generic defaults. A developer-specific `settings.local.json` may be placed beside the project file; it is ignored by Git and copied to the build output for local development.

## Build

```powershell
dotnet build RustDeskCompanion.csproj -c Release
```

The executable is produced under `bin\Release\net9.0-windows\`.

## License

This project is intended as an open-source companion around the RustDesk client. RustDesk itself remains under its own license and is not bundled here.
