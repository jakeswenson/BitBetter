ARG BITWARDEN_TAG
FROM ${BITWARDEN_TAG}

COPY bin/Debug/netcoreapp2.0/publish/* /bitBetter/
COPY ./.keys/cert.cert /newLicensing.cer

RUN set -e; set -x; \
    dotnet /bitBetter/bitBetter.dll && \
    mv /app/Core.dll /app/Core.orig.dll && \
    mv /app/modified.dll /app/Core.dll && \
    rm -rf /bitBetter && rm -rf /newLicensing.cer
