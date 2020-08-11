FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env-base
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - && \
    echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list && \
    apt update && apt install -y --no-install-recommends nodejs npm yarn

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-focal AS result-env-base
RUN apt-get update

FROM build-env-base as build-env
WORKDIR /app
# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore --disable-parallel

# Copy everything else and build
WORKDIR /app/drohub-vue
COPY ./drohub-vue/package.json ./drohub-vue/yarn.lock ./
RUN PATH=$(yarn global bin):$PATH; echo $PATH; yarn install

# Copy everything else and build
COPY ./drohub-vue ./
WORKDIR /app
COPY wwwroot/images/assets /app/wwwroot/images/assets
WORKDIR /app/drohub-vue
RUN PATH=$(yarn global bin):$PATH; yarn build --mode=development

WORKDIR /app/
COPY . ./
RUN dotnet publish -c Release -o out -p:InformationalVersion=$(cat .git/$(cat .git/HEAD | cut -d' ' -f2)) ./DroHub.csproj

FROM result-env-base
RUN apt-get install -y janus-tools ffmpeg
# Build runtime image
RUN mkdir -p /opt/keys/
WORKDIR /app
COPY appsettings.json /app/
COPY wwwroot /app/wwwroot
COPY --from=build-env /app/wwwroot/vue ./wwwroot/vue
COPY --from=build-env /app/out .

ENV ASPNETCORE_ENVIRONMENT="Production"
ENTRYPOINT ["dotnet", "DroHub.dll"]