FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /bitBetter

COPY . /bitBetter
COPY cert.cert /app/

RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "/app/bitBetter.dll"]