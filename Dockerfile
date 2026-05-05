# syntax=docker/dockerfile:1

#
# Rinha runs on linux/amd64. Being explicit avoids accidental cross-arch builds.
#
ARG TARGETPLATFORM=linux/amd64

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

#
# Copy only project metadata first to maximize NuGet restore cache hits.
# (Source code changes won't invalidate restore layers.)
#
COPY ./FraudDetection.slnx ./
COPY ./src/FraudDetection.Api/FraudDetection.Api.csproj ./src/FraudDetection.Api/
COPY ./src/FraudDetection.Preprocess/FraudDetection.Preprocess.csproj ./src/FraudDetection.Preprocess/

RUN dotnet restore ./src/FraudDetection.Preprocess/FraudDetection.Preprocess.csproj

#
# ReadyToRun can improve cold start but may increase image size / working set.
# Keep it configurable; default is off to stay safer under the 350MB total cap.
#
ARG PUBLISH_R2R=false
RUN dotnet restore ./src/FraudDetection.Api/FraudDetection.Api.csproj \
    -r linux-x64 \
    -p:PublishReadyToRun=$PUBLISH_R2R

COPY ./src ./src

RUN dotnet publish ./src/FraudDetection.Preprocess/FraudDetection.Preprocess.csproj \
    -c Release \
    -o /out/preprocess \
    --no-restore

RUN dotnet publish ./src/FraudDetection.Api/FraudDetection.Api.csproj \
    -c Release \
    -r linux-x64 \
    -o /out/api \
    --no-restore \
    --self-contained false \
    -p:PublishReadyToRun=$PUBLISH_R2R

# Preprocess dataset during build (produces data/references.bin + data/labels.bin).
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:9.0-bookworm-slim AS preprocess
WORKDIR /work

COPY --from=build /out/preprocess ./preprocess
COPY ./resources/references.json.gz ./resources/references.json.gz

# Ensure the output directory exists even if the tool changes.
RUN mkdir -p /work/data \
    && dotnet ./preprocess/FraudDetection.Preprocess.dll ./resources/references.json.gz /work/data

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

