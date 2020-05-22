FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env

WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-focal
RUN apt-get update && apt-get install -y janus-tools
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "MJR-Converter.dll"]