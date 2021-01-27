FROM ubuntu:20.04
RUN apt-get update && apt-get -y install curl gnupg2
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - && \
    echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list && \
    apt update && apt install -y --no-install-recommends nodejs npm yarn

RUN PATH=$(yarn global bin):$PATH; echo $PATH; yarn install
ENV TZ=Europe/Amsterdam
COPY ./entrypoint.sh /usr/bin/entrypoint
RUN chmod 755 /usr/bin/entrypoint
WORKDIR /home/cirrus
ENTRYPOINT ["entrypoint"]