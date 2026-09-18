FROM debian:bookworm
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y samba winbind ldb-tools \
    && rm -rf /var/lib/apt/lists/*
COPY setup.sh /lab/ad/setup.sh
CMD ["sh", "/lab/ad/setup.sh"]
