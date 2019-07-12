using System;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Support.V4.App;

namespace Obsidian.DroidD
{
    public class ServiceNotificationHelper
    {
        /// <summary>
        /// Code for 'no icon', e.g. in notification actions.
        /// </summary>
        const int NoIcon = 0;

        readonly Context _context;
        readonly Type _serviceType;
        readonly string _serviceNotificationChannelId;
        readonly int _serviceNotificationId;
        readonly string _notificationTitle;
        public ServiceNotificationHelper(Context context, Type serviceType, string serviceNotificationChannelId, int serviceNotificationId, string notificationTitle)
        {
            _context = context;
            _serviceType = serviceType;
            _serviceNotificationChannelId = serviceNotificationChannelId;
            _serviceNotificationId = serviceNotificationId;
            _notificationTitle = notificationTitle;
        }




        public void CreateNotificationChannel(string notificationChannelId, string notificationChannelName)
        {
            var channel = new NotificationChannel(notificationChannelId, notificationChannelName, NotificationImportance.Default);
            channel.LightColor = Color.Blue;
            channel.LockscreenVisibility = NotificationVisibility.Private;
            var notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }

        public Notification CreateNotification(string contentText, NotificationCompat.Action[] actions = null)
        {
            var builder = new NotificationCompat.Builder(_context, _serviceNotificationChannelId)
                .SetContentTitle(_notificationTitle)
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Drawable.obsidian_logo)
                .SetContentIntent(CreateShowMainActivityIntent())
                .SetOngoing(true)
                .SetOnlyAlertOnce(true);

            if (actions != null)
            {
                foreach (var a in actions)
                    builder.AddAction(a);
            }
            return builder.Build();
        }

        public void UpdateNotification(string contentText, NotificationCompat.Action[] actions = null)
        {
            var notification = CreateNotification(contentText, actions);
            var notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService);
            notificationManager.Notify(_serviceNotificationId, notification);
        }

        public void RemoveNotification()
        {
            var notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService);
            notificationManager.Cancel(_serviceNotificationId);
        }

        public NotificationCompat.Action CreateAction(string actionConstant, string actionTitle, int iconResource = NoIcon)
        {
            var stopServiceIntent = new Intent(_context, _serviceType);
            stopServiceIntent.SetAction(actionConstant);

            var stopServicePendingIntent = PendingIntent.GetService(_context, 0, stopServiceIntent, 0);

            return new NotificationCompat.Action.Builder(iconResource, actionTitle, stopServicePendingIntent).Build();
        }

        /// <summary>
        /// Create a PendingIntent that will take the user back to MainActivity when tapping on the notification.
        /// </summary>
        /// <returns>PendingIntent</returns>
        PendingIntent CreateShowMainActivityIntent()
        {
            // https://stackoverflow.com/questions/16885706/click-on-notification-to-go-current-activity/21424959#21424959
            var notificationIntent = new Intent(_context, typeof(MainActivity));
           // notificationIntent.SetFlags(ActivityFlags.SingleTop);
            return PendingIntent.GetActivity(_context, 0, notificationIntent, 0);
        }

    }
}