FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN rm -f drohub.db && \
    dotnet publish -c Release -o out -p:InformationalVersion=$(cat .git/$(cat .git/HEAD | cut -d' ' -f2)) && \
    dotnet ef database update && \
    cp drohub.db ./out/ && \
    rm ./appsettings.Development.json

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
RUN apt-get update && apt-get install -y sqlite3
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/docker-dotnet/wait-for-it.sh .
COPY --from=build-env /app/docker-dotnet/docker-dotnet-run-entrypoint.sh .
COPY --from=build-env /app/wwwroot/AdminLTE/bower_components ./wwwroot/AdminLTE/bower_components/
ENV ASPNETCORE_ENVIRONMENT="Production"
ENTRYPOINT ["dotnet", "DroHub.dll"]
