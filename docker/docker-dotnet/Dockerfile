FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env

WORKDIR /app
# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
WORKDIR /app/drohub-vue
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - && \
    echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list && \
    apt update && apt install -y --no-install-recommends nodejs npm yarn

RUN PATH=$(yarn global bin):$PATH; echo $PATH; yarn install && yarn build --mode=development
WORKDIR /app/
RUN dotnet publish -c Release -o out -p:InformationalVersion=$(cat .git/$(cat .git/HEAD | cut -d' ' -f2)) ./DroHub.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-focal

RUN apt-get update && apt-get install -y janus-tools ffmpeg yarn
RUN mkdir -p /opt/keys/
WORKDIR /app
COPY --from=build-env /app/wwwroot/vue ./wwwroot/vue
COPY --from=build-env /app/out .
COPY --from=build-env /app/wwwroot/AdminLTE/bower_components ./wwwroot/AdminLTE/bower_components/

ENV ASPNETCORE_ENVIRONMENT="Production"
ENTRYPOINT ["dotnet", "DroHub.dll"]