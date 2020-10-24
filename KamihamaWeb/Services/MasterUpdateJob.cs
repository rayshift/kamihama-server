using System.Threading.Tasks;
using KamihamaWeb.Interfaces;
using Quartz;
using Serilog;

namespace KamihamaWeb.Services
{
    public class MasterUpdateJob: IJob
    {
        private readonly IMasterSingleton _master;
        public MasterUpdateJob(IMasterSingleton master)
        {
            _master = master;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            Log.Information("[SCHEDULE] Running MasterUpdateJob (master update).");
            if (_master.IsReady)
            {
                await _master.RunUpdate();
            }
            else
            {
                Log.Information("[SCHEDULE] Skipping MasterUpdateJob, service not ready.");
            }
        }
    }
}