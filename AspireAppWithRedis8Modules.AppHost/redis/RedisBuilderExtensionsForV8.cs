using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace QCMS.AppHost.redis;
public static class RedisBuilderExtensionsForV8
{
    internal const string PrimaryEndpointName = "tcp";

    public static IResourceBuilder<RedisResource> AddRedisV8(
       this IDistributedApplicationBuilder builder,
       [ResourceName] string name,
       int? port = null,
       string? configFilePath = null,
       IResourceBuilder<ParameterResource>? password = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        // StackExchange.Redis doesn't support passwords with commas.
        // See https://github.com/StackExchange/StackExchange.Redis/issues/680 and
        // https://github.com/Azure/azure-dev/issues/4848 
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        var redis = new RedisResource(name, passwordParameter);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(redis, async (@event, ct) =>
        {
            connectionString = await redis.GetConnectionStringAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{redis.Name}' resource but the connection string was null.");
            }
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks().AddRedis(sp => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"), name: healthCheckKey);

        return builder.AddResource(redis)
                      .WithEndpoint(port: port, targetPort: 6379, name: PrimaryEndpointName)
                      .WithImage(RedisContainerImageTags.Image, RedisContainerImageTags.Tag)
                      .WithImageRegistry(RedisContainerImageTags.Registry)
                      .WithHealthCheck(healthCheckKey)
                      // see https://github.com/dotnet/aspire/issues/3838 for why the password is passed this way
                      .WithEntrypoint("/bin/sh")
                      .WithEnvironment(context =>
                      {
                          if (redis.PasswordParameter is { } password)
                          {
                              context.EnvironmentVariables["REDIS_PASSWORD"] = password;
                          }
                      })
                      .WithArgs(context =>
                      {
                          var redisCommand = new List<string>
                          {
                              // Aspire : Addes path to config file to the command
                              string.IsNullOrEmpty(configFilePath) ? "redis-server" : $"redis-server {configFilePath}"
                          };

                          if (redis.PasswordParameter is not null)
                          {
                              redisCommand.Add("--requirepass");
                              redisCommand.Add("$REDIS_PASSWORD");
                          }

                          if (redis.TryGetLastAnnotation<PersistenceAnnotation>(out var persistenceAnnotation))
                          {
                              var interval = (persistenceAnnotation.Interval ?? TimeSpan.FromSeconds(60)).TotalSeconds.ToString(CultureInfo.InvariantCulture);

                              redisCommand.Add("--save");
                              redisCommand.Add(interval);
                              redisCommand.Add(persistenceAnnotation.KeysChangedThreshold.ToString(CultureInfo.InvariantCulture));
                          }

                          context.Args.Add("-c");
                          context.Args.Add(string.Join(' ', redisCommand));

                          return Task.CompletedTask;
                      });
    }

    private sealed class PersistenceAnnotation(TimeSpan? interval, long keysChangedThreshold) : IResourceAnnotation
    {
        public TimeSpan? Interval => interval;
        public long KeysChangedThreshold => keysChangedThreshold;
    }
}


public static class RedisContainerImageTags
{
    public const string Image = "redis"; // Replace "redis" with the actual image name if different.  
    public const string Tag = "latest"; // Replace "latest" with the actual tag if different.  
    public const string Registry = "docker.io"; // Add Registry if required by the code.  
}
