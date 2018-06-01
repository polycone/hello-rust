# Hello Rust
This is a reverse-engineering tool written for the early Rust video game version (released in 2014). This tool provides a UDP proxying mechanism, thus it can interact with UDP datagrams. The tool uses Lua scripts to research and modify game network packets.

## Configuration
There is a tool configuration file `configuration.json`

| Key | Description |
| --- | --- |
| `nat_life_time` | The proxy will keep endpoints (client socket and proxy client socket) binding during this time (in ms)  |
| `remote_ip` | Remote server address |
| `remote_port` | Remote server port |
| `local_port` | Local listener port |
| `mtu` | UDP datagram buffer size |
