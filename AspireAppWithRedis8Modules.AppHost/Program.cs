using QCMS.AppHost.redis;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedisV8("db", configFilePath: "/etc/redis/redis-full.conf")
    .WithDockerfile("redis")
    .WithRedisInsight()
    .WithEndpoint("tcp", endpoint =>
    {
        endpoint.Port = 6379;
        endpoint.TargetPort = 6379;
    });


builder.Build().Run();
