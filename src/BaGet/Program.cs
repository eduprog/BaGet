using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


/* 
    - Publicando o projeto Nuget Server 
    dotnet publish -c Release -r win-x64 --self-contained true --property:PublishDir=C:\trab\devel\publish\NugetServer

    - Adicionar eSistem NugetServer como serviço do windows
    sc.exe create "eSistem - NugetServer" binpath="C:\trab\devel\publish\NugetServer\BaGet.exe" start= auto

    - Inciando o serviço do windows 
    sc.exe start "eSistem - NugetServer"

    - Parando o serviço do windows
    sc.exe stop "eSistem - NugetServer"

    - Removendo dos serviços do windows
    sc.exe delete "eSistem - NugetServer"


Para funcionar em https, não esquecer que o dns tem de apontar o endereço correto, ou seja.

O domínio que está configurado como por exemplo o nuget.esistem.com.br deverá constar no host da máquina de dev.
Caso contrário haverá um erro de sistema.

Seguir os passos de https://dylanbeattie.net/2020/11/18/using-https-with-kestrel.html Funcionando perfeito.

Verificar o funcionamento que está no eSistemLoja.Api pois talvez o erro seja somente de domínio e a geração do
certificado. Lembrar que o certificado é gerado para um determinado domínio, ou determinados domínios.


Usar Certificado da https://app.zerossl.com/dashboard free por 90 dias e pode ir renovando.

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
                    //web.UseUrls(
                    //    //$"http://*:61437",
                    //    $"https://*:61438");
                    web
                        //.UseUrls()
                        //.UseKestrel()
                        .ConfigureKestrel(options =>
                    {
                        var port = 61438;
                        // Remove the upload limit from Kestrel. If needed, an upload limit can
                        // be enforced by a reverse proxy server, like IIS.
                        //options.Limits.MaxRequestBodySize = null;
                        //options.ListenAnyIP(61437);
                        options.Listen(IPAddress.Any, port, listenOptions =>
                        {
                            var certPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "certificate.pfx");
                            //////var keyPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "key.pem");
                            var password = "#eSistem2023@";
                            //var cert = new X509Certificate2(certPath);
                            //var connectionOptions = new HttpsConnectionAdapterOptions();
                            //connectionOptions.ServerCertificate = cert;
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            listenOptions.UseHttps(certPath,password);
                        });
                    });

                    web.UseStartup<Startup>();
                });

            return host;
        }
    }
}
