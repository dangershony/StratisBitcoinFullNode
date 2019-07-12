using System;
using Android.Content;
using Android.OS;

namespace Obsidian.DroidD
{
    public class NodeServiceConnection : Java.Lang.Object, IServiceConnection
    {
        public Lazy<NodeController> NodeControllerFactory { get; private set; } = new Lazy<NodeController>();

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            // We've bound to NodeService.NodeServiceBinder, cast the IBinder and get LocalService instance
            NodeService.NodeServiceBinder binder = (NodeService.NodeServiceBinder)service;
            NodeControllerFactory = binder.NodeControllerFactory;
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            
        }
    }
}