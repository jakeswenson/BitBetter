ARG BITWARDEN_TAG
FROM ${BITWARDEN_TAG}

COPY bin/Release/net10.0/publish/* /bitBetter/
COPY ./.keys/cert.cert /newLicensing.cer

RUN set -e; set -x; \
    if [ -f /app/Api ]; then EXEC=Api; else EXEC=Identity; fi && \
    dotnet /bitBetter/bitBetter.dll /newLicensing.cer /app/$EXEC && \
    rm -f /app/$EXEC && \
    printf '#!/bin/sh\nexec dotnet /app/%s.dll "$@"\n' "$EXEC" > /app/$EXEC && \
    chmod +x /app/$EXEC && \
    rm -rf /bitBetter /newLicensing.cer
