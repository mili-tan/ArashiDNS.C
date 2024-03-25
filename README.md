<p align="center">
  <img src='https://mili.one/pics/arashic.png' width="70%" height="70%"/>
</p>

----------
The super easy way DNS over HTTPS Client

```
wget https://t.mili.one/arashic-linux-x64 -O /usr/bin/arashic
wget https://t.mili.one/arashic.service -O /etc/systemd/system/arashic@.service
chmod +x /usr/bin/arashic 
systemctl enable arashic@arashi.eu.org --now
```
OR using Docker. `docker run -d -p 127.0.0.1:53:53 -p 127.0.0.1:53:53/udp ghcr.io/mili-tan/arashidns.c https://arashi.eu.org/dns-query -l 0.0.0.0` 
```
Usage: ArashiDNS.C [options] <target> <backup>

Arguments:
  target                     Target DNS over HTTPS service URL
  backup                     Backup DNS over HTTPS service URL

Options:
  -?|-h|--help               Show help information.
  -l|--listen <IPEndPoint>   Set server listening address and port
  -w <Timeout>               Timeout time to wait for reply
  -n                         Do not use embedded cache
  -e                         Do not add EDNS Client Subnet
  -h2                        Force HTTP/2
  -h3                        Force HTTP/3 (requires libmsquic)
  -log                       Print query and response logs
  --ecs-address <IPAddress>  Force override EDNS client subnet address
  --startup-dns <IPAddress>  The startup dns address for resolving the DoH server address
```

HTTP/3 support on Linux requires libmsquic, see [how to install it](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Quic/readme.md#Linux).

## License

Copyright (c) 2020 Milkey Tan. Code released under the [Mozilla Public License 2.0](https://www.mozilla.org/en-US/MPL/2.0/). 

<sup>ArashiDNS™ is a trademark of Milkey Tan.</sup>
