# ArashiDNS.C
The super easy way DNS over HTTPS Client

```
wget https://github.com/mili-tan/ArashiDNS.C/releases/latest/download/arashic-linux-x64 -O /usr/bin/arashic
wget https://raw.githubusercontent.com/mili-tan/ArashiDNS.C/main/arashic%40.service -O /etc/systemd/system/arashic@.service
chmod +x /usr/bin/arashic 
systemctl enable arashic@dns.cloudflare.com --now
```

```
Usage: ArashiDNS.C [options] <url>

Arguments:
  url                       Target DNS over HTTPS service URL

Options:
  -?|-h|--help              Show help information.
  -l|--listen <IPEndPoint>  Set server listening address and port
  -w <timeout>              Timeout time to wait for reply
  -n                        Do not use embedded cache
```
