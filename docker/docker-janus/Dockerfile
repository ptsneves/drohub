FROM ubuntu:20.04
RUN apt-get update && apt-get -y install curl && curl -sL -o /etc/apt/trusted.gpg.d/morph027-janus.asc https://packaging.gitlab.io/janus/gpg.key
RUN . /etc/lsb-release; echo "deb [arch=amd64] https://packaging.gitlab.io/janus/$DISTRIB_CODENAME $DISTRIB_CODENAME main" | tee /etc/apt/sources.list.d/morph027-janus.list
RUN apt-get update && apt-get install -y janus vim janus-demos
RUN rm /etc/janus/*
COPY ./configs/* /etc/janus/
ENTRYPOINT ["janus"]