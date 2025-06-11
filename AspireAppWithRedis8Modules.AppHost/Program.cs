var builder = DistributedApplication.CreateBuilder(args);

var redis = builder
    .AddRedis("db")
    .WithImageTag(tag: "8")
    .WithArgs(context =>
    {
        context.Args[1] = $"{context.Args[1]} --loadmodule /usr/local/lib/redis/modules/redisearch.so --loadmodule /usr/local/lib/redis/modules/rejson.so";
    })
    .WithRedisInsight();

builder.Build().Run();
