FROM bitbetter/api

COPY bin/Debug/netcoreapp2.0/publish/* /app/

ENTRYPOINT [ "dotnet", "/app/licenseGen.dll", "--core", "/app/Core.dll", "--cert", "/cert.pfx" ]