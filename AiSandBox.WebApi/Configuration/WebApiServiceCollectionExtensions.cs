namespace AiSandBox.WebApi.Configuration;

public static class WebApiServiceCollectionExtensions
{
    public static IServiceCollection AddWebApiPresentationServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();


        return services;
    }
}

