using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet
{
  public class Program
  {
    [Option(Description = "SQL Connection Url", ShortName = "s", ShowInHelpText = true)]
    public static string SqlConnectionUrl { get; } = "SslMode=none;server=localhost;port=3306;database=albion;user=root;password=";

    [Option(Description = "Max age in Days of returned orders", ShortName = "a", ShowInHelpText = true)]
    [Range(1, 30)]
    public static int MaxAge { get; } = 3;

    [Option(Description = "Enable Debug Logging", ShortName = "d", LongName = "debug", ShowInHelpText = true)]
    public static bool Debug { get; }

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
