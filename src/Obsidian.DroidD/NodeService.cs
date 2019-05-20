using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.DroidD.Node;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging.Xamarin;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;
using Binder = Android.OS.Binder;

namespace Obsidian.DroidD
{
    [Service]
    public class NodeService : Service
    {
        /// <summary>
        /// Intent Action for external callers to start the foreground service.
        /// </summary>
        public const string ActionStartForegroundService = nameof(ActionStartForegroundService);

        /// <summary>
        /// Channel Id used to create and update the channel.
        /// </summary>
        const string NodeNotificationChannelId = nameof(NodeNotificationChannelId);

        /// <summary>
        /// Channel name used in the notification settings of the app (notification categories).
        /// </summary>
        const string NotificationChannelName = "Node Status";

        /// <summary>
        /// Channel title in the notification drawer.
        /// </summary>
        const string NotificationTitle = "Obsidian DroidD";

        

        const int NoIcon = 0;
        const int ServiceRunningNotificationId = 485987;

        const string ActionStopForegroundService = nameof(ActionStopForegroundService);
        const string ActionStartNode = nameof(ActionStartNode);
        const string ActionStopNode = nameof(ActionStopNode);

        IFullNode _fullNode;
        string[] _startParameters;
        public event EventHandler FullNodeStopRequested;
        public event EventHandler LogMessageReceived;
        public bool IsForeGroundServiceCreated;


        public override IBinder OnBind(Intent intent)
        {
            return new NodeServiceBinder(this);
        }

        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            Notification notification = null;

            switch (intent.Action)
            {
                case ActionStartForegroundService:

                    _startParameters = intent.GetStringArrayListExtra("startParameters").ToArray();
                    CreateNotificationChannel(NodeNotificationChannelId, NotificationChannelName);

                    notification = CreateNotification(NodeNotificationChannelId, "Foreground service created.", new[] { CreateAction(ActionStartNode, "Start Node"), CreateAction(ActionStopForegroundService, "Stop Foreground Service") });
                    IsForeGroundServiceCreated = true;
                    break;

                case ActionStopForegroundService:
                    StopForeground(true);
                    StopSelf();
                    IsForeGroundServiceCreated = false;
                    break;

                case ActionStartNode:
                    StartFullNode(_startParameters);
                    notification = CreateNotification(NodeNotificationChannelId, "Running Node.", new[] { CreateAction(ActionStopNode, "Stop Node") });
                    break;

                case ActionStopNode:
                    FullNodeStopRequested?.Invoke(this, EventArgs.Empty);
                    notification = CreateNotification(NodeNotificationChannelId, "Node stopped.", new[] { CreateAction(ActionStartNode, "Start Node"), CreateAction(ActionStopForegroundService, "Stop Foreground Service") });
                    break;
            }

            if (notification != null)
                StartForeground(ServiceRunningNotificationId, notification);
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            IsForeGroundServiceCreated = false;
        }


        void StartFullNode(string[] startParameters)
        {
            PosBlockHeader.CustomPoWHash = ObsidianHash.GetObsidianPoWHash;

            try
            {
                var nodeSettings = new NodeSettings(networksSelector: ObsidianNetworksSelector.Obsidian,
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, StratisNode", args: startParameters)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                IFullNodeBuilder builder = new FullNodeBuilder();

                builder = builder.UseNodeSettings(nodeSettings);
                builder = builder.UseBlockStore();
                builder = builder.UsePosConsensus();
                builder = builder.UseMempool();
                builder = builder.UseColdStakingWallet();
                builder = builder.AddPowPosMining();
                //.UseApi()
                builder = builder.UseApps();
                builder = builder.AddRPC();
                _fullNode = builder.Build();

                XamarinLogger.EntryAdded += (sender, e) => LogMessageReceived?.Invoke(this, e);

                if (_fullNode != null)
                    Task.Run(async () => await RunAsync(_fullNode));

            }
            catch (Exception ex)
            {
                Console.WriteLine(@"There was a problem initializing or running the node. Details: '{0}'", ex.Message);
            }
        }

        static string GetName()
        {
#if DEBUG
            return $"Obsidian.DroidD {Assembly.GetEntryAssembly()?.GetName().Version} (Debug)";
#else
			return $"ObsidianD {Assembly.GetEntryAssembly()?.GetName().Version} (Release)";
#endif
        }

        /// <summary>
        /// Installs handlers for graceful shutdown in the console, starts a full node and waits until it terminates.
        /// </summary>
        /// <param name="node">Full node to run.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        async Task RunAsync(IFullNode node)
        {
            var done = new ManualResetEventSlim(false);
            using (var cts = new CancellationTokenSource())
            {
                Action shutdown = () =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        Console.WriteLine("Application is shutting down.");
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException exception)
                        {
                            Console.WriteLine(exception.Message);
                        }
                    }

                    done.Wait();
                };

                FullNodeStopRequested += (sender, eventArgs) =>
                {
                    shutdown();
                };

                try
                {
                    await node.RunAsync(cts.Token, "Application started. Press Ctrl+C to shut down.", "Application stopped.").ConfigureAwait(false);
                }
                finally
                {
                    done.Set();
                    _fullNode = null;
                }
            }
        }

        void CreateNotificationChannel(string notificationChannelId, string notificationChannelName)
        {
            var channel = new NotificationChannel(notificationChannelId, notificationChannelName, NotificationImportance.Default);
            channel.LightColor = Color.Blue;
            channel.LockscreenVisibility = NotificationVisibility.Private;
            var notificationManager = (NotificationManager)GetSystemService(Context.NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }

        Notification CreateNotification(string notificationChannelId, string contentText, NotificationCompat.Action[] actions)
        {
            var builder = new NotificationCompat.Builder(this, notificationChannelId)
                .SetContentTitle(NotificationTitle)
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Drawable.obsidian_logo)
                .SetContentIntent(CreateShowMainActivityIntent())
                .SetOngoing(true);

            foreach (var a in actions)
                builder.AddAction(a);

            return builder.Build();
        }

        NotificationCompat.Action CreateAction(string actionConstant, string actionTitle)
        {
            var stopServiceIntent = new Intent(this, GetType());
            stopServiceIntent.SetAction(actionConstant);

            var stopServicePendingIntent = PendingIntent.GetService(this, 0, stopServiceIntent, 0);

            return new NotificationCompat.Action.Builder(NoIcon, actionTitle, stopServicePendingIntent).Build();
        }

        /// <summary>
        /// Create a PendingIntent that will take the user back to MainActivity when tapping on the notification.
        /// </summary>
        /// <returns>PendingIntent</returns>
        PendingIntent CreateShowMainActivityIntent()
        {
            return PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.UpdateCurrent);
        }

        public class NodeServiceBinder : Binder
        {
            public readonly NodeService NodeService;

            public NodeServiceBinder(NodeService nodeService)
            {
                NodeService = nodeService;
            }
        }
    }
}