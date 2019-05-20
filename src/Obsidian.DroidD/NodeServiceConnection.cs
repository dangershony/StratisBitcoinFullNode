using Android.Content;
using Android.OS;

namespace Obsidian.DroidD
{
    public class NodeServiceConnection : Java.Lang.Object, IServiceConnection
    {
        readonly MainActivity _mainActivity;

        public bool IsConnected { get; private set; }
        public NodeService.NodeServiceBinder Binder { get; private set; }

        public NodeServiceConnection(MainActivity mainActivity)
        {
            _mainActivity = mainActivity;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            Binder = service as NodeService.NodeServiceBinder;
            IsConnected = Binder != null;
            if (IsConnected)
                _mainActivity.OnNodeServiceConnected();
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            _mainActivity.OnNodeServiceDisconnecting();
            IsConnected = false;
            Binder = null;
           
        }
    }
}