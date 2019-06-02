using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Binder = Android.OS.Binder;

namespace Obsidian.DroidD
{
    [Service]
    public class NodeService : Service
    {
        #region Constants used by the foreground service

        /// <summary>
        /// Intent Action for external callers to start the foreground service.
        /// </summary>
        public const string ActionStartNodeService = nameof(ActionStartNodeService);

        /// <summary>
        /// Intent Action for external callers to start the foreground service.
        /// </summary>
        public const string ActionStopNodeService = nameof(ActionStopNodeService);

        /// <summary>
        /// Channel Id used to create and update the channel.
        /// </summary>
        const string NodeServiceNotificationChannelId = nameof(NodeServiceNotificationChannelId);

        /// <summary>
        /// An identifier for this notification unique within the application.
        /// </summary>
        const int NodeServiceNotificationId = 485987;

        /// <summary>
        /// Channel name used in the notification settings of the app (notification categories).
        /// </summary>
        const string NotificationChannelName = "Node Status";

        /// <summary>
        /// Channel title in the notification drawer.
        /// </summary>
        const string NotificationTitle = "Block Store";



        #endregion

        readonly string _tag = nameof(NodeService);
        ServiceNotificationHelper _helper;
        NodeController _nodeController;
        Handler _handler;
        Action _runnable;
        bool _tryRestart = true;

        public override void OnCreate()
        {
            base.OnCreate();
            _helper = new ServiceNotificationHelper(this, typeof(NodeService), NodeServiceNotificationChannelId, NodeServiceNotificationId,
                NotificationTitle);
            _helper.CreateNotificationChannel(NodeServiceNotificationChannelId, NotificationChannelName);

            _handler = new Handler();

            _runnable = () =>
            {
                if (_handler == null || _runnable == null)
                    return;

                if (_nodeController != null)
                {
                    string msg = $"Height: {_nodeController.GetBlockStoreInfo().height} | Synced: {_nodeController.GetBlockStoreInfo().synced}";
                    _helper.UpdateNotification(msg, new[] { _helper.CreateAction(ActionStopNodeService, "Stop Node") });
                }

                _handler.PostDelayed(_runnable, 15000);
            };
            _handler.PostDelayed(_runnable, 15000);
            this.Info("OnCreate() finished...");
        }

        public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            this.Info($"In OnStartCommand(), Action: {intent.Action}...");
            switch (intent.Action)
            {
                case ActionStartNodeService:
                    var notification = _helper.CreateNotification("Starting Service and node...");
                    StartForeground(NodeServiceNotificationId, notification);
                    this.Info($"Called StartForeground...");
                    if (_nodeController == null)
                    {
                        this.Info($"NodeControllerFactory was null, creating it...");
                        _nodeController = new NodeController();
                        _nodeController.NodeCrashed += OnNodeCrashed;
                        _nodeController.StartFullNode(intent.GetStringArrayListExtra("startParameters").ToArray());
                        this.Info($"NodeControllerFactory.StartFullNode() has returned...");
                        _helper.UpdateNotification("Started service and node.", new[] { _helper.CreateAction(ActionStopNodeService, "Stop Node") });
                    }
                    else
                    {
                        this.Info($"NodeControllerFactory was not null (already running)...");
                        _helper.UpdateNotification("Node was already running.");
                    }
                    break;

                case ActionStopNodeService:
                    _handler?.RemoveCallbacks(_runnable);
                    if (_nodeController != null)
                    {
                        this.Info($"Calling NodeControllerFactory.RaiseNodeShutdownRequested()...");
                        _nodeController.RaiseNodeShutdownRequested();
                        _helper.UpdateNotification("Stopping Node.");
                    }
                    else
                    {
                        this.Info($"NodeControllerFactory was null (already stopped)...");
                        _helper.UpdateNotification("Node was already stopped.");
                    }
                    this.Info(_tag, $"Calling StopForeground(true), StopSelf()...");
                    _tryRestart = false;
                    StopForeground(true);
                    StopSelf();
                    break;
            }

            return StartCommandResult.Sticky;
        }

        void OnNodeCrashed(object sender, EventArgs e)
        {
            this.Info($"Error: In OnNodeCrashed(), calling StopForeground(true), StopSelf()...");
            _helper.UpdateNotification("Node crashed!");
            StopForeground(true);
            StopSelf();
        }

        public override void OnDestroy()
        {
            this.Info($"In OnDestroy()...");
            _nodeController?.RaiseNodeShutdownRequested();
            _nodeController = null;
            _helper.RemoveNotification();
            if (_tryRestart)
            {
                var broadcastIntent = new Intent(this, typeof(RestartOnDestroyReceiver));
                SendBroadcast(broadcastIntent);
            }
            _handler.RemoveCallbacks(_runnable);
            _handler.Dispose();
            _handler = null;
            base.OnDestroy();
        }

        public override IBinder OnBind(Intent intent)
        {
            return new NodeServiceBinder(new Lazy<NodeController>(() => _nodeController));
        }

        public class NodeServiceBinder : Binder
        {
            public Lazy<NodeController> NodeControllerFactory;

            public NodeServiceBinder(Lazy<NodeController> nodeController)
            {
                NodeControllerFactory = nodeController;
            }
        }
    }
}