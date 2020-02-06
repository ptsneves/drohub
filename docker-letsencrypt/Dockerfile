FROM ubuntu:18.04
RUN export DEBIAN_FRONTEND=noninteractive && apt-get update && apt-get install -y python-pip && pip install pyopenssl certbot-plugin-gandi
ADD ./gandi.ini .
COPY letsencrypt.sh /usr/bin/
ENTRYPOINT [ "/usr/bin/letsencrypt.sh" ]