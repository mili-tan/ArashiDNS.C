# ArashiDNS.C
The super easy way DNS over HTTPS Client

```
wget https://t.mili.one/arashic-linux-x64 -O /usr/bin/arashic
wget https://t.mili.one/arashic.service -O /etc/systemd/system/arashic@.service
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
  -e                        Do not add EDNS Client Subnet
  -h2                       Force HTTP/2
  -h3                       Force HTTP/3 (requires libmsquic)
```

HTTP/3 support on Linux requires libmsquic, see [how to install it](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Quic/readme.md#Linux).
