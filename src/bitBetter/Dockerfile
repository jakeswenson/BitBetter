FROM bitwarden/api

COPY bin/Debug/netcoreapp2.0/publish/* /bitBetter/
COPY ./.keys/cert.cert /newLicensing.cer

RUN dotnet /bitBetter/bitBetter.dll && \
    echo "modified dll" && \
    mv /app/Core.dll /app/Core.orig.dll && \
    mv /app/modified.dll /app/Core.dll && \
    echo "replaced dll" && \
    rm -rf /bitBetter && rm -rf /newLicensing.cer && \
    echo "cleaned up"
