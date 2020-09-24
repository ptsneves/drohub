#!/usr/bin/env bash
set -e
if [ -z ${DOMAIN} ]; then
    >&2 echo "Error: DOMAIN env variable not set. Failing."
    exit 1
fi

if [ ${DOMAIN} = "localhost" ]; then
    echo "DOMAIN is localhost. Skipping"
    exit 0
fi

if [ -z ${GANDI_USER} ]; then
    >&2 echo "Error: GANDI_USER env variable not set. Failing."
    exit 1
fi

if [ -z ${GANDI_KEY} ]; then
    >&2 echo "Error: GANDI_KEY env variable not set. Failing."
    exit 1
fi

umask 077
echo "certbot_plugin_gandi:dns_api_key=${GANDI_KEY}" > gandi.ini
echo "Creating certificates for ${DOMAIN} and www.${DOMAIN}"
certbot certonly \
    -a certbot-plugin-gandi:dns \
    --certbot-plugin-gandi:dns-credentials gandi.ini \
    -d ${DOMAIN} \
    -d www.${DOMAIN} \
    --noninteractive \
    --agree-tos \
    --expand \
    -m ${GANDI_USER}