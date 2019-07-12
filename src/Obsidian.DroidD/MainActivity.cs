using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

namespace Obsidian.DroidD
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : AppCompatActivity
    {
        TextView _logView;
        ScrollView _scrollView;
        FloatingActionButton _buttonRefresh;
        NodeServiceConnection _nodeServiceConnection;

        // https://stackoverflow.com/questions/19569546/background-service-without-activity
        // https://fabcirablog.weebly.com/blog/creating-a-never-ending-background-service-in-android
        // https://developerlife.com/2017/07/10/android-o-n-and-below-component-lifecycles-and-background-tasks/
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            _buttonRefresh = FindViewById<FloatingActionButton>(Resource.Id.fab);
            _buttonRefresh.Visibility = ViewStates.Visible;
            _buttonRefresh.Click += (s, e) => Update();


            _logView = (TextView)FindViewById(Resource.Id.logtext);
            _scrollView = (ScrollView)FindViewById(Resource.Id.scroller);

            this.Info("Calling StartDroidNodeServiceIfNotRunning()...");
            StartDroidNodeServiceIfNotRunning();
            _nodeServiceConnection = new NodeServiceConnection();
            this.Info("Calling BindService()...");
            bool isBindServiceResultTrue = BindService(new Intent(this, typeof(NodeService)), _nodeServiceConnection, Bind.None);
            if (!isBindServiceResultTrue)
            {
                this.Info("BindService() returned false, returning!");
                return;
            }
            this.Info("BindService() returned true.");
        }

        void StartDroidNodeServiceIfNotRunning()
        {
            if (this.IsOwnServiceRunning(typeof(NodeService)))
            {
                this.Info("Detected NodeService, returning...");
                return;
            }

            this.Info("Did not detect NodeService, calling StartForegroundService...");

            var intent = new Intent(this, typeof(NodeService));
            intent.PutStringArrayListExtra("startParameters", NodeConfig.StartParameters);
            intent.SetAction(NodeService.ActionStartNodeService);
            StartForegroundService(intent);
        }

        void Update()
        {
            // TODO: move the linebuffer into the service,
            // so that we can use the default activity mode. A new instance can then also Bind to the current generation of the service. Then, the reference to the bound service will not become invalid.
            try
            {
                this.Info("Fetching log...");
                NodeController controller = _nodeServiceConnection.NodeControllerFactory.Value;
                if (controller != null)
                {
                    var log = controller.GetLog();
                    if (!string.IsNullOrEmpty(log))
                    {
                        var oldText = _logView.Text;
                        var oldLines = CountLines(oldText);
                        if (oldLines > 200)
                        {
                            var shorter = DeleteLines(oldText, 100);
                            var newText = shorter + log;
                            _logView.Text = newText;
                            this.Info("Truncated log...");
                        }
                        else
                        {
                            _logView.Text = oldText + log;
                        }

                        _scrollView.FullScroll(FocusSearchDirection.Down);
                    }
                }
            }
            catch (Exception e)
            {
                this.Info($"Error in Update(): {e.Message}");
            }
            finally
            {
                _nodeServiceConnection = null;
                //GC.Collect(0);
            }

        }

        static string DeleteLines(
            string stringToRemoveLinesFrom,
            int numberOfLinesToRemove,
            bool startFromBottom = false)
        {
            string[] allLines = stringToRemoveLinesFrom.Split(
                separator: System.Environment.NewLine.ToCharArray(),
                options: StringSplitOptions.RemoveEmptyEntries);
            string toReturn;
            if (startFromBottom)
                toReturn = string.Join(System.Environment.NewLine, allLines.Take(allLines.Length - numberOfLinesToRemove));
            else
                toReturn = string.Join(System.Environment.NewLine, allLines.Skip(numberOfLinesToRemove));
            return toReturn;
        }

        static int CountLines(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (str == string.Empty)
                return 0;
            int index = -1;
            int count = 0;
            while (-1 != (index = str.IndexOf(System.Environment.NewLine, index + 1, StringComparison.Ordinal)))
                count++;

            return count + 1;
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}

