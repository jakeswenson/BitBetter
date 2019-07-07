FROM mcr.microsoft.com/dotnet/core/sdk:2.1 as build

WORKDIR /licenseGen

COPY . /licenseGen

RUN set -e; set -x; \
	dotnet add package Newtonsoft.Json --version 12.0.1 \
	&& dotnet restore \
	&& dotnet publish


FROM bitbetter/api

COPY --from=build /licenseGen/bin/Debug/netcoreapp2.0/publish/* /app/

ENTRYPOINT [ "dotnet", "/app/licenseGen.dll", "--core", "/app/Core.dll", "--cert", "/cert.pfx" ]
