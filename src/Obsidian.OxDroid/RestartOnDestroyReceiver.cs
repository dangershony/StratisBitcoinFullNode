using Android.Content;

namespace Obsidian.DroidD
{
    [BroadcastReceiver(Enabled = true, Exported = true, Label = nameof(RestartOnDestroyReceiver))]
    public class RestartOnDestroyReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var serviceType = typeof(NodeService);
            this.Info($"Received broadcast: {serviceType.Name} is being destroyed, restarting.");
            var startServiceIntent = new Intent(context, serviceType);
            startServiceIntent.PutStringArrayListExtra("startParameters", NodeConfig.StartParameters);
            startServiceIntent.SetAction(NodeService.ActionStartNodeService);
            context.StartForegroundService(startServiceIntent);
        }
    }
}