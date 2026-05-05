# syntax=docker/dockerfile:1

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY ./FraudDetection.slnx ./
COPY ./src ./src

RUN dotnet restore ./src/FraudDetection.Api/FraudDetection.Api.csproj

RUN dotnet publish ./src/FraudDetection.Preprocess/FraudDetection.Preprocess.csproj -c Release -o /out/preprocess --no-restore
RUN dotnet publish ./src/FraudDetection.Api/FraudDetection.Api.csproj -c Release -o /out/api --no-restore \
    -p:PublishReadyToRun=true

# Preprocess dataset during build (produces data/references.bin + data/labels.bin).
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:9.0-bookworm-slim AS preprocess
WORKDIR /work

COPY --from=build /out/preprocess ./preprocess
COPY ./resources/references.json.gz ./resources/references.json.gz

RUN dotnet ./preprocess/FraudDetection.Preprocess.dll ./resources/references.json.gz ./data

# Final runtime image
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /out/api ./
COPY --from=preprocess /work/data ./data
COPY ./resources/normalization.json ./resources/normalization.json
COPY ./resources/mcc_risk.json ./resources/mcc_risk.json

EXPOSE 8080
ENTRYPOINT ["dotnet", "FraudDetection.Api.dll"]

