FROM ubuntu:focal

USER root

RUN set -o xtrace \
    && apt-get update \
    && apt-get install -y openjdk-8-jdk wget unzip zip sudo \
    && echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

ENV APP_PATH="parrot-backend" \
    RPC_API_PATH="RPCInterfaces" \
    LICENSES_PATH="/usr/share/android/licenses"

COPY ./entrypoint.sh /usr/bin/entrypoint
RUN chmod 755 /usr/bin/entrypoint \
    && mkdir -p ${LICENSES_PATH}

COPY licenses/android-sdk-license ${LICENSES_PATH}/
ENTRYPOINT [ "entrypoint" ]


