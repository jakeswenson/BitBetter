FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
WORKDIR /licenseGen

COPY . /licenseGen
COPY Core.dll /app/
COPY cert.pfx /app/

RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/sdk:6.0
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT [ "dotnet", "/app/licenseGen.dll", "--core", "/app/Core.dll", "--cert", "/app/cert.pfx" ]
