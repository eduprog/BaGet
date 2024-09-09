using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Serilog;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

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
            var logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "log.txt");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()  // Write logs to the console
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)  // Write logs to a file
                .CreateLogger();


            var host = Host
                .CreateDefaultBuilder(args)
                //.ConfigureServices((context, services) =>
                //{
                //    services.AddLogging();
                //})
                .UseSerilog()
                .ConfigureAppConfiguration((ctx, config) =>
                {
                    var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

                    if (!string.IsNullOrEmpty(root))
                    {
                        config.SetBasePath(root);
                    }
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();  // Clear other logging providers
                    // Serilog is already set up, so no need to add Console or File here
                    logging.SetMinimumLevel(LogLevel.Debug); // Ajuste conforme necessário
                })
                .ConfigureWebHostDefaults((web) =>
                {
                    //web.UseUrls(
                    //    //$"http://*:61437",
                    //    $"https://*:61438");
                    web
                        //.UseUrls()
                        //.UseKestrel()
                        .ConfigureKestrel((context, options) =>
                    {
                        var port = 61438;
                        // Remove the upload limit from Kestrel. If needed, an upload limit can
                        // be enforced by a reverse proxy server, like IIS.
                        //options.Limits.MaxRequestBodySize = null;
                        //options.ListenAnyIP(61437);
                        options.Listen(IPAddress.Any, port, listenOptions =>
                        {
                            var config = context.Configuration;

                            var certPath = config["Certificado:PathFileCrt"];
                            var keyPath = config["Certificado:PathFileKey"];
                            var certPfx = config["Certificado:PathFilePfx"];
                            var password = config["Certificado:Senha"];




                            //var certPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "nuget.esistem.com.br.pfx");
                            //var certPath = config.Configuration["Certificado:PathFileCrt"];
                            //////var keyPath = Path.Combine(AppContext.BaseDirectory, "Certificados", "key.pem");
                            //var password = config.Configuration["Certificado:Senha"];//"#eSistem2023@";
                            //var cert = new X509Certificate2(certPath);
                            //var connectionOptions = new HttpsConnectionAdapterOptions();
                            //connectionOptions.ServerCertificate = cert;

                            if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(keyPath))
                            {
                                var certificate = LoadCertificate(certPath, keyPath, password);
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                listenOptions.UseHttps(certificate);
                            }
                            else if (!string.IsNullOrEmpty(certPfx) && !string.IsNullOrEmpty(password))
                            {
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                                listenOptions.UseHttps(certPfx, password);
                            }
                            else
                            {
                                throw new Exception("Certificado não configurado corretamente.");
                            }
                        });
                    }).UseStartup<Startup>();

                });

            return host;
        }


        public static X509Certificate2 LoadCertificate(string certPath, string keyPath, string password)
        {
            // Leitura do certificado .crt
            var certPem = File.ReadAllText(certPath);

            // Leitura da chave .key
            var keyPem = File.ReadAllText(keyPath);

            // Carregar o certificado
            var certParser = new PemReader(new StringReader(certPem));
            var cert = (X509Certificate)certParser.ReadObject();

            // Carregar a chave privada
            var keyParser = new PemReader(new StringReader(keyPem));

            var keyObject = keyParser.ReadObject();
            RsaPrivateCrtKeyParameters privateKey;

            switch (keyObject)
            {
                case AsymmetricCipherKeyPair keyPair:
                    privateKey = keyPair.Private as RsaPrivateCrtKeyParameters;
                    break;
                case RsaPrivateCrtKeyParameters rsaPrivateKey:
                    privateKey = rsaPrivateKey;
                    break;
                default:
                    throw new InvalidCastException("Unsupported key type.");
            }


            //var keyPair = (AsymmetricCipherKeyPair)keyParser.ReadObject();
            //var privateKey = keyPair.Private as RsaPrivateCrtKeyParameters;

            // Converter chave para o formato que o X509Certificate2 pode aceitar
            var rsa = RSA.Create();
            if (privateKey != null)
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = privateKey.Modulus.ToByteArrayUnsigned(),
                    Exponent = privateKey.PublicExponent.ToByteArrayUnsigned(),
                    P = privateKey.P.ToByteArrayUnsigned(),
                    Q = privateKey.Q.ToByteArrayUnsigned(),
                    DP = privateKey.DP.ToByteArrayUnsigned(),
                    DQ = privateKey.DQ.ToByteArrayUnsigned(),
                    InverseQ = privateKey.QInv.ToByteArrayUnsigned(),
                    D = privateKey.Exponent.ToByteArrayUnsigned()
                });

            // Criar o certificado X509
            var certBytes = cert.GetEncoded();
            var certificate = new X509Certificate2(certBytes);

            // Adicionar a chave privada ao certificado
            var certWithKey = certificate.CopyWithPrivateKey(rsa);

            return certWithKey;
        }
    }
}
