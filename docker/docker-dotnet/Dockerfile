FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out -p:InformationalVersion=$(cat .git/$(cat .git/HEAD | cut -d' ' -f2)) ./DroHub.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=build-env /app/wwwroot/AdminLTE/bower_components ./wwwroot/AdminLTE/bower_components/
ENV ASPNETCORE_ENVIRONMENT="Production"
ENTRYPOINT ["dotnet", "DroHub.dll"]
