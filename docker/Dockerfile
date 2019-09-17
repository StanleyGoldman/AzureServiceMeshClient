FROM ubuntu:16.04

ENV DEBIAN_FRONTEND=noninteractive
RUN echo "APT::Get::Assume-Yes \"true\";" > /etc/apt/apt.conf.d/90assumeyes

RUN apt-get update \
&& apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        git \
        iputils-ping \
        libcurl3 \
        libicu55 \
        libunwind8 \
        netcat

WORKDIR /azp

COPY ./vsts-agent-linux-x64-2.155.1.tar.gz .
RUN mkdir /azp/agent
RUN tar -xvzf vsts-agent-linux-x64-2.155.1.tar.gz -C /azp/agent

COPY ./start.sh .
RUN chmod +x start.sh

CMD ["./start.sh"]