using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Stratis.Bitcoin.Configuration.Logging.Xamarin;

namespace Obsidian.DroidD
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : AppCompatActivity
    {
        TextView _logView;
        ScrollView _scrollView;
        // https://fabcirablog.weebly.com/blog/creating-a-never-ending-background-service-in-android
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            _logView = (TextView)FindViewById(Resource.Id.logtext);
            _scrollView = (ScrollView)FindViewById(Resource.Id.scroller);
        }

        protected override void OnResume()
        {
            base.OnResume();
            XamarinLogger.EntryAdded += XamarinLogger_EntryAdded;
        }

        protected override void OnPause()
        {
            base.OnPause();
            XamarinLogger.EntryAdded -= XamarinLogger_EntryAdded;
        }

        void XamarinLogger_EntryAdded(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                var data = (XamarinLogger.XamarinLoggerEventArgs)e;
                if (data.LogLevel != Microsoft.Extensions.Logging.LogLevel.Information && data.LogLevel != Microsoft.Extensions.Logging.LogLevel.Warning && data.LogLevel != Microsoft.Extensions.Logging.LogLevel.Error && data.LogLevel != Microsoft.Extensions.Logging.LogLevel.Critical)
                    return;
                if (_logView.Text.Length > 50000)
                    _logView.Text = "";
                _logView.Text += data.Text;
                _scrollView.FullScroll(FocusSearchDirection.Down);
            });
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        void FabOnClick(object sender, EventArgs eventArgs)
        {
            StartDroidNodeService();
            //View view = (View) sender;
            //Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
            //    .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }

        void StartDroidNodeService()
        {
            var startParameters = new[] { "-addnode=165.22.90.248" };
            var intent = new Intent(this, typeof(DroidNodeService));
            intent.PutStringArrayListExtra("startParameters", startParameters);
            intent.SetAction(DroidNodeService.ActionStartForegroundService);
            StartForegroundService(intent);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}

