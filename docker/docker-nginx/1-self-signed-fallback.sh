#!/bin/sh
set -e

if [ -z ${DOMAIN_NAME} ]; then
    >&2 echo "Error: DOMAIN_NAME env variable not set. Failing."
    exit 1
fi

if [ -z ${SSL_CERTIFICATE_PATH} ]; then
    >&2 echo "Error: SSL_CERTIFICATE_PATH env variable not set. Failing."
    exit 1
fi

if [ -z ${SSL_CERTIFICATE_KEY_PATH} ]; then
    >&2 echo "Error: SSL_CERTIFICATE_KEY_PATH env variable not set. Failing."
    exit 1
fi

if [ ! -f ${SSL_CERTIFICATE_KEY_PATH} -o ! -f ${SSL_CERTIFICATE_PATH} ]; then
    >&2 echo "Error: SSL_CERTIFICATE_KEY_PATH or SSL_CERTIFICATE_PATH not found. Falling back to self signed localhost key"
    install -D /usr/share/ca-certificates/fullchain.pem ${SSL_CERTIFICATE_PATH}
    install -D /usr/share/ca-certificates/privkey.pem ${SSL_CERTIFICATE_KEY_PATH}
fi