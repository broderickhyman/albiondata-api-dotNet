using AlbionData.Models;
using Elastic.Apm.NetCoreAll;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.OpenApi.Models;

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
      services.AddDbContext<MainContext>(opt => opt.UseMySql(Program.SqlConnectionUrl));
      services.AddMvc();
      services.AddCors();

      services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Albion Online Data API", Version = "v1" }));
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
        app.UseAllElasticApm(Configuration);
      }
      app.UseSwagger(x => x.RouteTemplate = "api/{documentName}/swagger.json");

      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/api/v1/swagger.json", "Albion Online Data API v1");
        c.SwaggerEndpoint("/api/v2/swagger.json", "Albion Online Data API v2");
        c.RoutePrefix = "api/swagger";
      });

      app.UseRouting();
      app.UseCors(builder => builder.AllowAnyOrigin());

      app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
  }
}
