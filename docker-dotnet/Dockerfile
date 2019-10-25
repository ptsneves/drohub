FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app
RUN apt-get install sqlite3
# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out -p:InformationalVersion=$(cat .git/$(cat .git/HEAD | cut -d' ' -f2)) && \
    rm ./appsettings.Development.json

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/docker-dotnet/wait-for-it.sh .
COPY --from=build-env /app/docker-dotnet/docker-dotnet-run-entrypoint.sh .
COPY --from=build-env /app/wwwroot/AdminLTE/bower_components ./wwwroot/AdminLTE/bower_components/
ENV ASPNETCORE_ENVIRONMENT="Production"
ENTRYPOINT ["dotnet", "DroHub.dll"]
