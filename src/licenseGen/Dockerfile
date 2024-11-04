FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /licenseGen

COPY . /licenseGen

RUN set -e; set -x; \
	dotnet add package Newtonsoft.Json --version 13.0.1 \
	&& dotnet restore \
	&& dotnet publish


FROM bitbetter/api

COPY --from=build /licenseGen/bin/Release/net8.0/publish/* /app/

ENTRYPOINT [ "dotnet", "/app/licenseGen.dll", "--core", "/app/Core.dll", "--cert", "/cert.pfx" ]
