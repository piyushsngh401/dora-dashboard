# Build context is the repo root (see render.yaml's dockerContext), because Api references
# Core and Ingestion as sibling projects — a Dockerfile scoped to src/Api alone couldn't see them.
#
# docker build -f src/Api/Dockerfile -t dora-dashboard-api .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, using just the project files, so this (slow) layer is cached across builds that
# only change source code, not dependencies.
COPY DoraDashboard.sln ./
COPY src/Core/Core.csproj src/Core/
COPY src/Ingestion/Ingestion.csproj src/Ingestion/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render (and most PaaS hosts) inject PORT at runtime; Kestrel needs to be told to bind to it.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
