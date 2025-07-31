using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace Jim.MinioOSS;

public static class MinioOSSExtensions
{
    public static IServiceCollection AddMinioOSS(this IServiceCollection services)
    {
        services.AddOptions<MinioOSSOptions>()
            .BindConfiguration("OSS");

        var ossOptions = services.BuildServiceProvider().GetService<IOptions<MinioOSSOptions>>()?.Value;

        services.AddMinio(configureClient => configureClient
            .WithEndpoint(ossOptions.Endpoint)
            .WithSSL(ossOptions.IsEnableHttps)
            .WithCredentials(ossOptions.AccessKey, ossOptions.SecretKey)
        .Build());

        services.AddSingleton<IMinioOSSManage, MinioOSSManage>();

        return services;
    }
}
