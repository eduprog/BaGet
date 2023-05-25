using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace BaGet
{
    public class eSistemLojaApiService : BackgroundService
    {
        public eSistemLojaApiService()
        {
           
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //Log.Information($"Serviço sendo executado. {DateTime.Now}");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

}
