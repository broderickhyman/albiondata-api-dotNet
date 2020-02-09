using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.ComponentModel.DataAnnotations;

namespace albiondata_api_dotNet
{
  public class Program
  {
    [Option(Description = "SQL Connection Url", ShortName = "s", ShowInHelpText = true)]
    public static string SqlConnectionUrl { get; set; } = "SslMode=none;server=localhost;port=3306;database=albion;user=root;password=";

    [Option(Description = "Max age in Hours of returned orders", ShortName = "a", ShowInHelpText = true)]
    [Range(1, 168)]
    public static int MaxAge { get; set; } = 24;

    [Option(Description = "Enable Debug Logging", ShortName = "d", LongName = "debug", ShowInHelpText = true)]
    public static bool Debug { get; set; }

    public static string[] args;

    public static void Main(string[] args)
    {
      Program.args = args;
      CommandLineApplication.Execute<Program>(args);
    }

    private void OnExecute()
    {
      CreateWebHostBuilder(args).Build().Run();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>();
  }
}
