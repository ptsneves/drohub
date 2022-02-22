FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-focal
RUN apt-get update && apt-get -y install curl gnupg2
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - && \
    echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list && \
    apt update && apt install -y --no-install-recommends nodejs npm yarn git make g++

ENV TZ=Europe/Amsterdam
ENV HOME=/.yarn/
COPY ./entrypoint.sh /usr/bin/entrypoint
RUN chmod 755 /usr/bin/entrypoint && \
    mkdir -p /.yarn && chmod a+rwx -R /.yarn && \
    mkdir -p /tmp/destdir/drohub-vue/node_modules && chmod a+rwx -R /tmp/destdir/
WORKDIR /home/cirrus
ENTRYPOINT ["entrypoint"]