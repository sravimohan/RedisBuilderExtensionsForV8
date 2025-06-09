FROM redis:8

ENV SKIP_FIX_PERMS=1
ENV DEBUG=1

COPY redis.conf /etc/redis/redis.conf
COPY redis-full.conf /etc/redis/redis-full.conf
CMD [ "redis-server", "/etc/redis/redis-full.conf" ]
