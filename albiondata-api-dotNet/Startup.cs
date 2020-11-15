using AlbionData.Models;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;

namespace albiondata_api_dotNet
{
  public enum ApiVersion
  {
    One,
    Two
  }

  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
      // needed to load configuration from appsettings.json
      services.AddOptions();

      // needed to store rate limit counters and ip rules
      services.AddMemoryCache();

      //load general configuration from appsettings.json
      services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));

      //load ip rules from appsettings.json
      services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));

      // inject counter and rules stores
      services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
      services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

      // https://github.com/aspnet/Hosting/issues/793
      // the IHttpContextAccessor service is not registered by default.
      // the clientId/clientIp resolvers use it.
      services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

      // configuration (resolvers, counter key builders)
      services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

      services.AddDbContext<MainContext>(opt => opt.UseMySql(Program.SqlConnectionUrl));
      services.AddControllersWithViews()
        .AddNewtonsoftJson()
        .AddXmlSerializerFormatters();
      services.AddCors();

      // Don't show v1 anymore as it shouldn't be used and all v1 has a v2
      //services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Albion Online Data API", Version = "v1" }));
      services.AddSwaggerGen(c => c.SwaggerDoc("v2", new OpenApiInfo { Title = "Albion Online Data API", Version = "v2" }));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      if (string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
      {
        app.UseDeveloperExceptionPage();
      }
      else
      {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
          ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });
      }
      app.UseSwagger(x => x.RouteTemplate = "api/{documentName}/swagger.json");

      // Order the endpoints from highest to lowest as the default shown is the first in line
      // Don't show v1 anymore as it shouldn't be used and all v1 has a v2
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/api/v2/swagger.json", "Albion Online Data API v2");
        //c.SwaggerEndpoint("/api/v1/swagger.json", "Albion Online Data API v1");
        c.RoutePrefix = "api/swagger";
      });

      app.UseIpRateLimiting();

      app.UseRouting();
      app.UseCors(builder => builder.AllowAnyOrigin());

      app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
  }
}
