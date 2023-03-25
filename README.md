## Kamihama Server

Custom server for serving localized assets for Magia Record to be used in conjunction with https://github.com/rayshift/magiatranslate.

## Build Instructions
Windows:
`dotnet publish -c Release KamihamaWeb`

Linux:
`dotnet publish -r linux-x64 -c Release KamihamaWeb`

## Config options
- `Proxy`: Leave empty or specify a HTTP proxy
- `MaxThreads`: Specify to client how many concurrent downloads can take place (higher number = more server load)
- `Version`: Minimum client version in client's `Config.h` to connect to this server (or an error will be shown to the user)
- `Type`: `master` or `replica` server (see load balancing)

### Load balancing
If you use Cloudflare and have the CF-IPCountry header set with a GeoIP country code you can load balance by specifying alternate endpoints, where the correct endpoint will be sent to the user on login:
```
"MagiRecoNodes": {
    "US": "https://us-endpoint.rayshift.io/game",
    "*": "https://global-endpoint.rayshift.io/game"
}
```
If the country code isn't found in the config file, the default (`*`) will be used.

## Usage
- Edit the configuration file appsettings(.Development).json using the example given in appsettings.Example.json
- Create a directory `MagiaRecoStatic/` in the binaries' working directory.
- Clone https://git.rayshift.io/kamihama/magia-assets.git or your own assets into this directory `git clone URL`.
- Run the server, either directly `./KamihamaWeb`, or through a systemd service:

/etc/systemd/system/kestrel-kamihama-prod.service
```
[Unit]
Description=Kamihama Server

[Service]
WorkingDirectory=/home/rayshift/KamihamaWeb/
ExecStart=/home/rayshift/KamihamaWeb/KamihamaWeb --urls http://0.0.0.0:6000
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kamihama-web
User=rayshift
Type=simple
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Bash file to update assets:
```
#!/bin/bash
cd /home/rayshift/KamihamaWeb
cd MagiRecoStatic/ && git pull --rebase origin master && cd .. && rm en_cache.json && sudo systemctl restart kestrel-kamihama-prod.service
```

## Contributing
Contributions are strongly encouraged as this server implementation will not receive further updates by its creator. Please create a pull request with your changes.
