# Debian private RustDesk test host

This deployment lets `j-hoef-debian` serve as both the Tailscale-only RustDesk
OSS server and RustDeskHop's private remote-desktop test target.

The server containers are pinned to RustDesk Server OSS `1.1.16`. Their ports
bind only to the Debian machine's Tailscale address (`100.108.6.62`), so they
are not exposed through its LAN or public interfaces. Only the desktop ports
needed by native RustDesk clients are published; web-client ports stay closed.

The `rustdeskhop-rustdesk-mode` helper switches the Debian RustDesk client
between the built-in public service and the private server. It backs up every
active `RustDesk2.toml`, changes only server-routing keys, and restores the
backup automatically if the RustDesk service fails to restart.

## Operations

```sh
cd /opt/rustdeskhop/rustdesk-server
sudo docker compose up -d
sudo rustdeskhop-rustdesk-mode private
sudo rustdeskhop-rustdesk-mode public
sudo rustdeskhop-rustdesk-mode status
```

Generated server keys and databases live only in
`/opt/rustdeskhop/rustdesk-server/data` on Debian and must not be committed.
Client configuration backups live in
`/var/lib/rustdeskhop/rustdesk-client/backups`.
