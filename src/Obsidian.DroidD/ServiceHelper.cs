using System;
using Android.App;
using Android.Content;

namespace Obsidian.DroidD
{
    public static class ServiceHelper
    {
        /// <summary>
        /// Detects if an own service started by the app is running.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="serviceType">The Type of the service.</param>
        /// <returns>True, if the service is running.</returns>
        public static bool IsOwnServiceRunning(this Context context, Type serviceType)
        {
            ActivityManager manager = (ActivityManager)context.GetSystemService(Context.ActivityService);
          
            var ownServices = manager.GetRunningServices(int.MaxValue);
            var serviceTypeName = Java.Lang.Class.FromType(serviceType).Name;
            foreach (var os in ownServices)
            {
                var foundName = os.Service.ClassName;
                if (foundName == serviceTypeName)
                    return true;
            }
            return false;
        }
    }
}