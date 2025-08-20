using Grapevine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    internal class WebServer
    {
        private static IRestServer? RestServer { get; set; }

        internal static string ServerPort { get; set; } = PortFinder.FindNextLocalOpenPort(1234);

        public static void Start()
        {
            if (RestServer != null && RestServer.IsListening) return;

            try
            {
                RestServer = RestServerBuilder.From<Startup>().Build();
                //RestServer = RestServerBuilder.UseDefaults().Build();

                RestServer.AfterStarting += (s) =>
                {                    
                    s.RouteScanner.Scan();
                    
                    //OpenBrowser(s.Prefixes.First());
                    OpenBrowser($"http://localhost:{WebServer.ServerPort}/");

                    Console.WriteLine("Web-Server gestartet.\r\n" + string.Join(' ', s.Prefixes));
                };

                RestServer.BeforeStopping += (s) =>
                {
                    Console.WriteLine("Web-Server beendet.");
                };

                RestServer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebServer.Start() " + ex.Message + Environment.NewLine + ex.StackTrace);
                throw;
            }
        }

        public static void Stop()
        {
            RestServer?.Stop();
        }

        public static void OpenBrowser(string url)
        {
            try
            {
#if DEBUG
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
#else
                Process.Start(url);
#endif
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    //Process.Start("xdg-open", url);

                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }

        }

    }

    internal class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; private set; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Warning);
            });

        }

        public void ConfigureServer(IRestServer server)
        {                        
            server.Prefixes.Add($"http://localhost:{WebServer.ServerPort}/");            
            server.Prefixes.Add($"http://+:{WebServer.ServerPort}/");
            

            /* Configure server to auto parse application/x-www-for-urlencoded data*/
            server.AutoParseFormUrlEncodedData();

            /* Configure Router Options (if supported by your router implementation) */
            server.Router.Options.SendExceptionMessages = true;

            /* TEST FÜR WEBSOCKETS If not response is sent, the router and server will not see this as an error condition. */
            server.Router.Options.RequireRouteResponse = false;

        }

    }

}
