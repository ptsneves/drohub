FROM nginx:stable

ADD fullchain.pem /usr/share/ca-certificates
ADD privkey.pem /usr/share/ca-certificates
COPY 1-self-signed-fallback.sh /docker-entrypoint.d/1-self-signed-fallback.sh
RUN chmod 700 /docker-entrypoint.d/1-self-signed-fallback.sh

COPY drohub.xyz.conf.template /etc/nginx/templates/default.conf.template