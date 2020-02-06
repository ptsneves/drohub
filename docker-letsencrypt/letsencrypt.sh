#!/usr/bin/env bash
while true; do
    certbot certonly \
        -a certbot-plugin-gandi:dns \
        --certbot-plugin-gandi:dns-credentials gandi.ini \
        -d drohub.xyz \
        -d www.drohub.xyz \
        --noninteractive \
        --agree-tos \
        --expand \
        -m ptsneves@gmail.com && \
        sleep 360000 # 10 hours
done;