using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian.Features.X1Wallet.Staking
{
    class StakingService
    {
        readonly Task stakingTask;
        readonly CancellationTokenSource cts;

        public StakingService()
        {
            this.cts = new CancellationTokenSource();
            this.stakingTask = new Task(DoWork, this.cts.Token);
        }



        public void Start()
        {
            if (this.stakingTask.Status != TaskStatus.Running)
                this.stakingTask.Start();

        }

        public void Stop()
        {

        }

        void DoWork()
        {

        }
    }

    class Caller
    {
        StakingService stakingService;

        void StartStaking()
        {
            if (this.stakingService == null)
            {
                this.stakingService = new StakingService();
                this.stakingService.Start();
            }
                
        }

        void StopStaking()
        {
            if (this.stakingService != null)
            {
                this.stakingService.Stop();
                this.stakingService = null;
            }
        }
    }
}
