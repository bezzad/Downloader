using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
internal class Startup
{
    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddControllers();
        services.AddHttpContextAccessor();
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline. 
    /// </summary>
    public void Configure(IApplicationBuilder app)
    {
        // app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = Handler });
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath =  "/Home/Error" });
        app.UseRouting();
        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
        });
    }
}