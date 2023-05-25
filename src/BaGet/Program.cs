using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


/* 
    - Publicando o projeto eSistemLoja.API
    dotnet publish -c Release -r win-x64 --self-contained true --property:PublishDir=../../distro/setup/publish/

    - Adicionar eSistemLojaAPI como serviço do windows
    sc.exe create "eSistemLojaAPI" binpath=".\Projeto\Setup\Publish\eSistemLoja.API.exe" start= auto

    - Inciando o serviço do windows eSistemLojaAPI
    sc.exe start "eSistemLojaAPI"

    - Parando o serviço do windows eSistemLojaAPI
    sc.exe stop "eSistemLojaAPI"

    - Removendo eSistemLojaAPI dos serviços do windows
    sc.exe delete "eSistemLojaAPI"
 */


namespace BaGet
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            if (!host.ValidateStartupOptions())
            {
                return;
            }

            var app = new CommandLineApplication
            {
                Name = "baget",
                Description = "A light-weight NuGet service",
            };

            app.HelpOption(inherited: true);

            app.Command("import", import =>
            {
                import.Command("downloads", downloads =>
                {
                    downloads.OnExecuteAsync(async cancellationToken =>
                    {
                        using (var scope = host.Services.CreateScope())
                        {
                            var importer = scope.ServiceProvider.GetRequiredService<DownloadsImporter>();

                            await importer.ImportAsync(cancellationToken);
                        }
                    });
                });
            });

            app.Option("--urls", "The URLs that BaGet should bind to.", CommandOptionType.SingleValue);

            app.OnExecuteAsync(async cancellationToken =>
            {
                await host.RunMigrationsAsync(cancellationToken);
                await host.RunAsync(cancellationToken);
            });

            await app.ExecuteAsync(args);
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var host = Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, config) =>
                {
                    var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

                    if (!string.IsNullOrEmpty(root))
                    {
                        config.SetBasePath(root);
                    }
                })
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseUrls(
                        //$"http://*:61437",
                        $"https://*:61438");
                    _ = web.ConfigureKestrel(options =>
                    {
                        // Remove the upload limit from Kestrel. If needed, an upload limit can
                        // be enforced by a reverse proxy server, like IIS.
                        options.Limits.MaxRequestBodySize = null;
                        //options.ListenAnyIP(61437);
                        options.ListenAnyIP(61438, cfg =>
                        {
                            var certPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "cert.pem");
                            var keyPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "key.pem");
                            var cert = new X509Certificate2(certPath);
                            cfg.UseHttps(cert);
                        });

                    });

                    web.UseStartup<Startup>();
                });

            return host;
        }
    }
}
