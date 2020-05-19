FROM ubuntu:20.04
RUN apt-get update && apt-get install -y janus vim janus-demos
RUN rm /etc/janus/*
COPY ./configs/* /etc/janus/
ENTRYPOINT ["janus"]