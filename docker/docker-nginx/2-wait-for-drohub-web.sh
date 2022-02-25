#!/bin/sh
set -e

if [ -z ${TIMEOUT_SECONDS} ]; then
    >&2 echo "Error: $TIMEOUT_SECONDS env variable not set. Failing."
    exit 1
fi

if [ -z ${DEPENDENCY_FQDN} ]; then
    >&2 echo "Error: DEPENDENCY_FQDN env variable not set. Failing."
    exit 1
fi

if [ -z ${DEPENDENCY_PORT} ]; then
    >&2 echo "Error: DEPENDENCY_FQDN env variable not set. Failing."
    exit 1
fi

while true; do
  timeout ${TIMEOUT_SECONDS} bash -c "</dev/tcp/${DEPENDENCY_FQDN}/${DEPENDENCY_PORT}" && exit 0
  sleep 5s
done