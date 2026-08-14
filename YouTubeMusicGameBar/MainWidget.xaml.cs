using System;
using System.Globalization;
using Microsoft.Gaming.XboxGameBar;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Foundation;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace YouTubeMusicGameBar
{
    public sealed partial class MainWidget : Page
    {
        private static readonly Uri YouTubeMusicHome = new Uri("https://music.youtube.com/");
        private static readonly Size DesktopMinWindowSize = new Size(240, 300);
        private static readonly Size DesktopMaxWindowSize = new Size(1600, 1000);
        private static readonly Size CompactMinWindowSize = new Size(464, 300);
        private static readonly Size CompactMaxWindowSize = new Size(900, 1000);
        private static readonly int[] ZoomPercentages =
            { 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200 };
        private const string UserAgentPreferenceKey = "UserAgentMode";
        private const string ZoomPreferenceKey = "ZoomPercentage";
        private const string MobileUserAgentPreference = "mobile";
        private const string DesktopUserAgentPreference = "desktop";
        private const string PlayMediaScript =
            "(() => { const media = document.querySelector('video, audio'); " +
            "if (!media) return false; void media.play(); return true; })()";
        private const string PauseMediaScript =
            "(() => { const media = document.querySelector('video, audio'); " +
            "if (!media) return false; media.pause(); return true; })()";
        private const string HideScrollBarsScript =
            "(() => { " +
            "const styleId = 'youtube-music-game-bar-hidden-scrollbars'; " +
            "const installStyle = () => { " +
            "if (!document.documentElement || document.getElementById(styleId)) return; " +
            "const style = document.createElement('style'); " +
            "style.id = styleId; " +
            "style.textContent = '* { scrollbar-width: none !important; -ms-overflow-style: none !important; } " +
            "*::-webkit-scrollbar { display: none !important; width: 0 !important; height: 0 !important; }'; " +
            "(document.head || document.documentElement).appendChild(style); " +
            "}; " +
            "if (document.readyState === 'loading') " +
            "document.addEventListener('DOMContentLoaded', installStyle, { once: true }); " +
            "else installStyle(); " +
            "})();";
        private const string MobileUserAgent =
            "Mozilla/5.0 (Linux; Android 10; K) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/151.0.0.0 Mobile Safari/537.36 EdgA/151.0.0.0";

        private XboxGameBarWidget _gameBarWidget;
        private Uri _pendingNavigationUri;
        private bool _initializationStarted;
        private bool _isInitialized;
        private bool _hasCompletedInitialNavigation;
        private bool _widgetEventsSubscribed;
        private bool _webViewEventsSubscribed;
        private bool _systemMediaControlsSubscribed;
        private bool _resourcesReleased;
        private bool _useMobileUserAgent;
        private int _zoomPercentage;
        private string _defaultUserAgent;
        private string _zoomDocumentScriptId;
        private CoreWebView2MemoryUsageTargetLevel? _currentMemoryUsageTargetLevel;
        private SystemMediaTransportControls _systemMediaControls;

        public MainWidget()
        {
            InitializeComponent();
            _useMobileUserAgent = LoadUserAgentPreference();
            _zoomPercentage = LoadZoomPreference();
            UpdateUserAgentMenuState();
            UpdateZoomControlState();
            Loaded += MainWidget_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs args)
        {
            base.OnNavigatedTo(args);
            _gameBarWidget = args.Parameter as XboxGameBarWidget;
            SubscribeToWidgetEvents();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs args)
        {
            ReleaseResources("page navigation");
            base.OnNavigatedFrom(args);
        }

        private async void MainWidget_Loaded(object sender, RoutedEventArgs args)
        {
            await InitializeWebViewAsync();
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            if (_resourcesReleased || _initializationStarted || _isInitialized)
            {
                return;
            }

            _initializationStarted = true;
            ShowLoadingState();
            DebugLog.Write("WebView2 initialisation started.");

            try
            {
                // WinUI 2's default UWP user-data folder is persistent package-local app data.
                // It is deliberately neither replaced nor deleted, so cookies and site storage persist.
                await MusicWebView.EnsureCoreWebView2Async();

                if (_resourcesReleased)
                {
                    _initializationStarted = false;
                    return;
                }

                if (MusicWebView.CoreWebView2 == null)
                {
                    throw new InvalidOperationException("WebView2 returned no CoreWebView2 instance.");
                }

                await ConfigureCoreWebViewAsync();
                _isInitialized = true;
                _initializationStarted = false;
                RefreshButton.IsEnabled = true;
                ZoomButton.IsEnabled = true;

                DebugLog.Write("WebView2 initialised successfully.");
                MusicWebView.Source = YouTubeMusicHome;
            }
            catch (Exception exception)
            {
                _initializationStarted = false;
                _isInitialized = false;
                DebugLog.Write("WebView2 initialisation failed: {0}", exception);
                if (_resourcesReleased)
                {
                    return;
                }

                ShowError(
                    "Microsoft Edge WebView2 Runtime could not be started.",
                    "Install or repair Microsoft Edge WebView2 Runtime and restart the widget.");
            }
        }

        private async System.Threading.Tasks.Task ConfigureCoreWebViewAsync()
        {
            CoreWebView2 core = MusicWebView.CoreWebView2;
            core.IsMuted = false;
            _defaultUserAgent = core.Settings.UserAgent;
            ApplyUserAgent(core);
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsZoomControlEnabled = false;
#if DEBUG
            core.Settings.AreDevToolsEnabled = true;
#else
            core.Settings.AreDevToolsEnabled = false;
#endif

            // This changes only the scrollbar's appearance. Overflow and scrolling
            // remain enabled for wheel, touch, keyboard, and controller input.
            await core.AddScriptToExecuteOnDocumentCreatedAsync(HideScrollBarsScript);
            await RegisterZoomDocumentScriptAsync(core);

            if (!_webViewEventsSubscribed)
            {
                core.NewWindowRequested += CoreWebView2_NewWindowRequested;
                core.IsDocumentPlayingAudioChanged += CoreWebView2_IsDocumentPlayingAudioChanged;
                core.IsMutedChanged += CoreWebView2_IsMutedChanged;
                _webViewEventsSubscribed = true;
            }

            UpdateWebViewMemoryUsageTarget(_gameBarWidget == null || _gameBarWidget.Visible);
            ConfigureSystemMediaControls();
        }

        private void ConfigureSystemMediaControls()
        {
            try
            {
                _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();
                _systemMediaControls.IsEnabled = true;
                _systemMediaControls.IsPlayEnabled = true;
                _systemMediaControls.IsPauseEnabled = true;
                _systemMediaControls.IsNextEnabled = false;
                _systemMediaControls.IsPreviousEnabled = false;
                if (!_systemMediaControlsSubscribed)
                {
                    _systemMediaControls.ButtonPressed += SystemMediaControls_ButtonPressed;
                    _systemMediaControlsSubscribed = true;
                }

                _systemMediaControls.DisplayUpdater.Type = MediaPlaybackType.Music;
                _systemMediaControls.DisplayUpdater.MusicProperties.Title = "YouTube Music";
                _systemMediaControls.DisplayUpdater.Thumbnail =
                    RandomAccessStreamReference.CreateFromUri(
                        new Uri("ms-appx:///Assets/MediaThumbnail.png"));
                _systemMediaControls.DisplayUpdater.Update();
                UpdateSystemMediaPlaybackStatus();
                DebugLog.Write("System media controls configured for background audio.");
            }
            catch (Exception exception)
            {
                DebugLog.Write("System media controls could not be configured: {0}", exception);
            }
        }

        private void UpdateSystemMediaPlaybackStatus()
        {
            if (_systemMediaControls == null || MusicWebView.CoreWebView2 == null)
            {
                return;
            }

            _systemMediaControls.PlaybackStatus = MusicWebView.CoreWebView2.IsDocumentPlayingAudio
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;
        }

        private async void SystemMediaControls_ButtonPressed(
            SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            string script;
            MediaPlaybackStatus requestedStatus;

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    script = PlayMediaScript;
                    requestedStatus = MediaPlaybackStatus.Playing;
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    script = PauseMediaScript;
                    requestedStatus = MediaPlaybackStatus.Paused;
                    break;
                default:
                    return;
            }

            await Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    try
                    {
                        if (_isInitialized && MusicWebView.CoreWebView2 != null)
                        {
                            await MusicWebView.CoreWebView2.ExecuteScriptAsync(script);
                            sender.PlaybackStatus = requestedStatus;
                        }
                    }
                    catch (Exception exception)
                    {
                        DebugLog.Write("System media command failed: {0}", exception);
                    }
                });
        }

        private void CoreWebView2_IsDocumentPlayingAudioChanged(CoreWebView2 sender, object args)
        {
            UpdateSystemMediaPlaybackStatus();
            DebugLog.Write("WebView2 audio-playing state changed: {0}", sender.IsDocumentPlayingAudio);
        }

        private void CoreWebView2_IsMutedChanged(CoreWebView2 sender, object args)
        {
            // Game Bar can hide the hosted window when its overlay is dismissed.
            // Do not allow that transition to mute WebView2 itself. This does not
            // alter YouTube's player mute state or the user's Windows mixer volume.
            EnsureWebViewAudioOutputEnabled(sender, "WebView2 mute-state change");
        }

        private void EnsureWebViewAudioOutputEnabled(CoreWebView2 core, string reason)
        {
            if (core == null || !core.IsMuted)
            {
                return;
            }

            try
            {
                core.IsMuted = false;
                DebugLog.Write("Cleared WebView2 host mute after {0}.", reason);
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not clear WebView2 host mute after {0}: {1}", reason, exception);
            }
        }

        private void UpdateWebViewMemoryUsageTarget(bool isVisible)
        {
            CoreWebView2 core = MusicWebView.CoreWebView2;
            if (core == null || _resourcesReleased)
            {
                return;
            }

            CoreWebView2MemoryUsageTargetLevel targetLevel = isVisible
                ? CoreWebView2MemoryUsageTargetLevel.Normal
                : CoreWebView2MemoryUsageTargetLevel.Low;

            if (_currentMemoryUsageTargetLevel == targetLevel)
            {
                return;
            }

            try
            {
                // Low is deliberately not WebView suspension: YouTube Music keeps
                // running scripts, networking, and audio while Game Bar is hidden.
                core.MemoryUsageTargetLevel = targetLevel;
                _currentMemoryUsageTargetLevel = targetLevel;
                DebugLog.Write("WebView2 memory target changed: {0}", targetLevel);
            }
            catch (Exception exception)
            {
                // Older Evergreen runtimes may not expose a newly added API even
                // though the application was compiled with a current SDK.
                DebugLog.Write("Could not change WebView2 memory target: {0}", exception);
            }
        }

        private static bool LoadUserAgentPreference()
        {
            try
            {
                object storedValue = ApplicationData.Current.LocalSettings.Values[UserAgentPreferenceKey];
                string storedMode = storedValue as string;
                return !string.Equals(storedMode, DesktopUserAgentPreference, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not read user-agent preference: {0}", exception);
                return true;
            }
        }

        private void SaveUserAgentPreference()
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[UserAgentPreferenceKey] =
                    _useMobileUserAgent ? MobileUserAgentPreference : DesktopUserAgentPreference;
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not save user-agent preference: {0}", exception);
            }
        }

        private void ApplyUserAgent(CoreWebView2 core)
        {
            // The mobile override changes presentation only. Desktop mode restores
            // the runtime's genuine default UA instead of spoofing another value.
            if (_useMobileUserAgent)
            {
                core.Settings.UserAgent = MobileUserAgent;
            }
            else if (!string.IsNullOrWhiteSpace(_defaultUserAgent))
            {
                core.Settings.UserAgent = _defaultUserAgent;
            }

            DebugLog.Write(
                "WebView2 user-agent mode applied: {0}",
                _useMobileUserAgent ? MobileUserAgentPreference : DesktopUserAgentPreference);
        }

        private void UpdateUserAgentMenuState()
        {
            MobileUserAgentMenuItem.IsChecked = _useMobileUserAgent;
            DesktopUserAgentMenuItem.IsChecked = !_useMobileUserAgent;
            ToolTipService.SetToolTip(
                UserAgentButton,
                _useMobileUserAgent ? "User agent: mobile layout" : "User agent: desktop layout");
        }

        private static int LoadZoomPreference()
        {
            try
            {
                object storedValue = ApplicationData.Current.LocalSettings.Values[ZoomPreferenceKey];
                int storedPercentage = storedValue is int ? (int)storedValue : 100;
                return ZoomPercentages[FindClosestZoomIndex(storedPercentage)];
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not read zoom preference: {0}", exception);
                return 100;
            }
        }

        private void SaveZoomPreference()
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[ZoomPreferenceKey] = _zoomPercentage;
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not save zoom preference: {0}", exception);
            }
        }

        private static int FindClosestZoomIndex(int percentage)
        {
            int closestIndex = 0;
            int closestDistance = Math.Abs(ZoomPercentages[0] - percentage);

            for (int index = 1; index < ZoomPercentages.Length; index++)
            {
                int distance = Math.Abs(ZoomPercentages[index] - percentage);
                if (distance < closestDistance)
                {
                    closestIndex = index;
                    closestDistance = distance;
                }
            }

            return closestIndex;
        }

        private string BuildZoomScript()
        {
            string zoomFactor = (_zoomPercentage / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            return
                "(() => { " +
                "if (window.top !== window) return; " +
                "const applyZoom = () => { " +
                "if (!document.documentElement) return; " +
                "document.documentElement.style.setProperty('zoom', '" + zoomFactor + "', 'important'); " +
                "}; " +
                "if (document.readyState === 'loading') " +
                "document.addEventListener('DOMContentLoaded', applyZoom, { once: true }); " +
                "else applyZoom(); " +
                "})();";
        }

        private async System.Threading.Tasks.Task RegisterZoomDocumentScriptAsync(CoreWebView2 core)
        {
            if (!string.IsNullOrEmpty(_zoomDocumentScriptId))
            {
                try
                {
                    core.RemoveScriptToExecuteOnDocumentCreated(_zoomDocumentScriptId);
                }
                catch (Exception exception)
                {
                    DebugLog.Write("Could not replace the previous zoom script: {0}", exception);
                }
            }

            _zoomDocumentScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildZoomScript());
        }

        private void UpdateZoomControlState()
        {
            int currentIndex = FindClosestZoomIndex(_zoomPercentage);
            string label = _zoomPercentage.ToString(CultureInfo.InvariantCulture) + "%";
            ZoomButton.Content = label;
            ZoomResetButton.Content = label;
            ZoomOutButton.IsEnabled = currentIndex > 0;
            ZoomInButton.IsEnabled = currentIndex < ZoomPercentages.Length - 1;
            ToolTipService.SetToolTip(ZoomButton, "Page zoom: " + label);
        }

        private async System.Threading.Tasks.Task ApplyZoomAsync(int percentage)
        {
            _zoomPercentage = ZoomPercentages[FindClosestZoomIndex(percentage)];
            UpdateZoomControlState();
            SaveZoomPreference();

            CoreWebView2 core = MusicWebView.CoreWebView2;
            if (!_isInitialized || core == null)
            {
                return;
            }

            try
            {
                await RegisterZoomDocumentScriptAsync(core);
                await core.ExecuteScriptAsync(BuildZoomScript());
                DebugLog.Write("Page zoom changed: {0}%", _zoomPercentage);
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not change page zoom: {0}", exception);
            }
        }

        private void MusicWebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (args.Exception == null)
            {
                DebugLog.Write("CoreWebView2Initialized event completed.");
            }
            else
            {
                DebugLog.Write("CoreWebView2Initialized event failed: {0}", args.Exception);
            }
        }

        private void MusicWebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            Uri destination;
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out destination))
            {
                args.Cancel = true;
                DebugLog.Write("Blocked malformed navigation URI.");
                return;
            }

            if (IsPlainHttpNavigation(destination))
            {
                args.Cancel = true;
                ReportBlockedHttpNavigation(destination);
                return;
            }

            if (!IsHttpsNavigation(destination))
            {
                args.Cancel = true;
                HandleExternalProtocolAsync(destination);
                return;
            }

            _pendingNavigationUri = destination;
            SetNavigationBusy(true);
            DebugLog.Write("Navigation starting: {0}", destination.GetLeftPart(UriPartial.Authority));
        }

        private void MusicWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            SetNavigationBusy(false);
            UpdateNavigationButtons();

            if (args.IsSuccess)
            {
                _hasCompletedInitialNavigation = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ErrorOverlay.Visibility = Visibility.Collapsed;
                EnsureWebViewAudioOutputEnabled(MusicWebView.CoreWebView2, "navigation completion");
                UpdateSystemMediaPlaybackStatus();
                DebugLog.Write("Navigation completed successfully.");
                return;
            }

            DebugLog.Write("Navigation failed: {0}", args.WebErrorStatus);

            bool authenticationNavigation = _pendingNavigationUri != null &&
                string.Equals(_pendingNavigationUri.Host, "accounts.google.com", StringComparison.OrdinalIgnoreCase);

            if (authenticationNavigation)
            {
                ShowError(
                    "Google sign-in did not complete",
                    "Google may restrict authentication in embedded browsers. The widget does not bypass or modify Google's sign-in security. You can retry, but availability is controlled by Google and WebView2.");
            }
            else
            {
                ShowError(
                    "YouTube Music could not be loaded",
                    "Check your internet connection and try again. WebView2 reported: " + args.WebErrorStatus + ".");
            }
        }

        private void MusicWebView_CoreProcessFailed(WebView2 sender, CoreWebView2ProcessFailedEventArgs args)
        {
            DebugLog.Write("WebView2 process failure: {0}, reason={1}", args.ProcessFailedKind, args.Reason);
            SetNavigationBusy(false);
            ShowError(
                "The YouTube Music browser process stopped",
                "Select Try again to restart the page. If this continues, repair Microsoft Edge WebView2 Runtime.");
        }

        private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;

            Uri destination;
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out destination))
            {
                DebugLog.Write("Ignored malformed new-window request.");
                return;
            }

            if (IsPlainHttpNavigation(destination))
            {
                ReportBlockedHttpNavigation(destination);
                return;
            }

            if (IsHttpsNavigation(destination))
            {
                DebugLog.Write("Redirecting new-window request into the existing WebView: {0}", destination.Host);
                MusicWebView.Source = destination;
                return;
            }

            HandleExternalProtocolAsync(destination);
        }

        private async void HandleExternalProtocolAsync(Uri destination)
        {
            string scheme = destination.Scheme.ToLowerInvariant();
            if (scheme != "mailto" && scheme != "tel")
            {
                DebugLog.Write("Ignored unsupported external protocol: {0}", scheme);
                return;
            }

            try
            {
                bool launched = await Launcher.LaunchUriAsync(destination);
                DebugLog.Write("External protocol {0} launch result: {1}", scheme, launched);
            }
            catch (Exception exception)
            {
                DebugLog.Write("External protocol launch failed: {0}", exception);
            }
        }

        private void ReportBlockedHttpNavigation(Uri destination)
        {
            DebugLog.Write("Blocked insecure HTTP navigation: {0}", destination.Host);
            SetNavigationBusy(false);
            ShowError(
                "Insecure connection blocked",
                "This widget only permits encrypted HTTPS pages. The plain HTTP request was not opened.");
        }

        private static bool IsHttpsNavigation(Uri destination)
        {
            return string.Equals(destination.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlainHttpNavigation(Uri destination)
        {
            return string.Equals(destination.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        }

        private void BackButton_Click(object sender, RoutedEventArgs args)
        {
            if (_isInitialized && MusicWebView.CanGoBack)
            {
                MusicWebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs args)
        {
            if (_isInitialized && MusicWebView.CanGoForward)
            {
                MusicWebView.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs args)
        {
            if (_isInitialized)
            {
                MusicWebView.Reload();
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs args)
        {
            if (_isInitialized)
            {
                MusicWebView.Source = YouTubeMusicHome;
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs args)
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            ShowLoadingState();

            if (!_isInitialized || MusicWebView.CoreWebView2 == null)
            {
                _initializationStarted = false;
                await InitializeWebViewAsync();
                return;
            }

            try
            {
                if (MusicWebView.Source == null)
                {
                    MusicWebView.Source = YouTubeMusicHome;
                }
                else
                {
                    MusicWebView.Reload();
                }
            }
            catch (Exception exception)
            {
                DebugLog.Write("WebView2 retry failed: {0}", exception);
                _isInitialized = false;
                _initializationStarted = false;
                await InitializeWebViewAsync();
            }
        }

        private void HideToolbarButton_Click(object sender, RoutedEventArgs args)
        {
            Toolbar.Visibility = Visibility.Collapsed;
            ToolbarRow.Height = new GridLength(0);
            ShowToolbarButton.Visibility = Visibility.Visible;
        }

        private void ShowToolbarButton_Click(object sender, RoutedEventArgs args)
        {
            ToolbarRow.Height = new GridLength(36);
            Toolbar.Visibility = Visibility.Visible;
            ShowToolbarButton.Visibility = Visibility.Collapsed;
        }

        private async void SizeMenuItem_Click(object sender, RoutedEventArgs args)
        {
            if (_gameBarWidget == null)
            {
                return;
            }

            MenuFlyoutItem menuItem = sender as MenuFlyoutItem;
            string requestedPreset = menuItem == null ? null : menuItem.Tag as string;
            Size requestedSize;

            switch (requestedPreset)
            {
                case "phone":
                    // 464x1000 is approximately the modern iPhone 19.5:9 portrait ratio
                    // while remaining inside Game Bar's Compact mode width limits.
                    requestedSize = new Size(464, 1000);
                    break;
                case "portrait":
                    requestedSize = new Size(600, 900);
                    break;
                case "square":
                    requestedSize = new Size(900, 900);
                    break;
                default:
                    return;
            }

            try
            {
                bool resized = await _gameBarWidget.TryResizeWindowAsync(requestedSize);
                DebugLog.Write(
                    "Preset resize requested: {0:0}x{1:0}, accepted={2}",
                    requestedSize.Width,
                    requestedSize.Height,
                    resized);
            }
            catch (Exception exception)
            {
                DebugLog.Write("Preset resize request failed: {0}", exception);
            }
        }

        private void UserAgentMenuItem_Click(object sender, RoutedEventArgs args)
        {
            ToggleMenuFlyoutItem menuItem = sender as ToggleMenuFlyoutItem;
            string requestedMode = menuItem == null ? null : menuItem.Tag as string;

            if (requestedMode != MobileUserAgentPreference && requestedMode != DesktopUserAgentPreference)
            {
                return;
            }

            bool requestedMobileMode = requestedMode == MobileUserAgentPreference;
            bool changed = requestedMobileMode != _useMobileUserAgent;
            _useMobileUserAgent = requestedMobileMode;
            UpdateUserAgentMenuState();
            SaveUserAgentPreference();

            if (!changed || !_isInitialized || MusicWebView.CoreWebView2 == null)
            {
                return;
            }

            ApplyUserAgent(MusicWebView.CoreWebView2);
            SetNavigationBusy(true);
            MusicWebView.Reload();
        }

        private async void ZoomOutButton_Click(object sender, RoutedEventArgs args)
        {
            int currentIndex = FindClosestZoomIndex(_zoomPercentage);
            if (currentIndex > 0)
            {
                await ApplyZoomAsync(ZoomPercentages[currentIndex - 1]);
            }
        }

        private async void ZoomResetButton_Click(object sender, RoutedEventArgs args)
        {
            await ApplyZoomAsync(100);
        }

        private async void ZoomInButton_Click(object sender, RoutedEventArgs args)
        {
            int currentIndex = FindClosestZoomIndex(_zoomPercentage);
            if (currentIndex < ZoomPercentages.Length - 1)
            {
                await ApplyZoomAsync(ZoomPercentages[currentIndex + 1]);
            }
        }

        private void SetNavigationBusy(bool isBusy)
        {
            ToolbarProgressRing.IsActive = isBusy;
            ToolbarProgressRing.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _isInitialized && MusicWebView.CanGoBack;
            ForwardButton.IsEnabled = _isInitialized && MusicWebView.CanGoForward;
            RefreshButton.IsEnabled = _isInitialized;
        }

        private void ShowLoadingState()
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            if (!_hasCompletedInitialNavigation)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ShowError(string title, string message)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ErrorTitle.Text = title;
            ErrorMessage.Text = message;
            ErrorOverlay.Visibility = Visibility.Visible;
        }

        private void SubscribeToWidgetEvents()
        {
            if (_gameBarWidget == null || _widgetEventsSubscribed)
            {
                return;
            }

            _gameBarWidget.VisibleChanged += GameBarWidget_VisibleChanged;
            _gameBarWidget.PinnedChanged += GameBarWidget_PinnedChanged;
            _gameBarWidget.WindowBoundsChanged += GameBarWidget_WindowBoundsChanged;
            _gameBarWidget.GameBarDisplayModeChanged += GameBarWidget_DisplayModeChanged;
            _gameBarWidget.CompactModeEnabledChanged += GameBarWidget_CompactModeEnabledChanged;
            _widgetEventsSubscribed = true;
            ConfigureWidgetSizing(_gameBarWidget);
        }

        private void UnsubscribeFromWidgetEvents()
        {
            if (_gameBarWidget == null || !_widgetEventsSubscribed)
            {
                return;
            }

            _gameBarWidget.VisibleChanged -= GameBarWidget_VisibleChanged;
            _gameBarWidget.PinnedChanged -= GameBarWidget_PinnedChanged;
            _gameBarWidget.WindowBoundsChanged -= GameBarWidget_WindowBoundsChanged;
            _gameBarWidget.GameBarDisplayModeChanged -= GameBarWidget_DisplayModeChanged;
            _gameBarWidget.CompactModeEnabledChanged -= GameBarWidget_CompactModeEnabledChanged;
            _widgetEventsSubscribed = false;
        }

        internal void ReleaseResources(string reason)
        {
            if (_resourcesReleased)
            {
                return;
            }

            _resourcesReleased = true;
            DebugLog.Write("Releasing widget resources after {0}.", reason);

            Loaded -= MainWidget_Loaded;
            UnsubscribeFromWidgetEvents();

            CoreWebView2 core = MusicWebView.CoreWebView2;
            if (core != null && _webViewEventsSubscribed)
            {
                core.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                core.IsDocumentPlayingAudioChanged -= CoreWebView2_IsDocumentPlayingAudioChanged;
                core.IsMutedChanged -= CoreWebView2_IsMutedChanged;
                _webViewEventsSubscribed = false;
            }

            if (_systemMediaControls != null)
            {
                try
                {
                    if (_systemMediaControlsSubscribed)
                    {
                        _systemMediaControls.ButtonPressed -= SystemMediaControls_ButtonPressed;
                        _systemMediaControlsSubscribed = false;
                    }

                    _systemMediaControls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    _systemMediaControls.IsEnabled = false;
                    _systemMediaControls.DisplayUpdater.ClearAll();
                }
                catch (Exception exception)
                {
                    DebugLog.Write("Could not release system media controls: {0}", exception);
                }

                _systemMediaControls = null;
            }

            MusicWebView.CoreProcessFailed -= MusicWebView_CoreProcessFailed;
            MusicWebView.CoreWebView2Initialized -= MusicWebView_CoreWebView2Initialized;
            MusicWebView.NavigationCompleted -= MusicWebView_NavigationCompleted;
            MusicWebView.NavigationStarting -= MusicWebView_NavigationStarting;

            try
            {
                // The widget has genuinely closed or navigated away at this point,
                // so releasing Chromium here cannot interrupt a merely hidden song.
                MusicWebView.Close();
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not close WebView2 cleanly: {0}", exception);
            }

            _currentMemoryUsageTargetLevel = null;
            _zoomDocumentScriptId = null;
            _gameBarWidget = null;
            _isInitialized = false;
        }

        private void ConfigureWidgetSizing(XboxGameBarWidget widget)
        {
            bool compactMode = widget.CompactModeEnabled;

            // Reassert these runtime values whenever Game Bar changes modes. Compact
            // mode can rebuild its chrome and otherwise lose the manifest defaults.
            widget.HorizontalResizeSupported = true;
            widget.VerticalResizeSupported = true;
            widget.MinWindowSize = compactMode ? CompactMinWindowSize : DesktopMinWindowSize;
            widget.MaxWindowSize = compactMode ? CompactMaxWindowSize : DesktopMaxWindowSize;
            DebugLog.Write(
                "Widget sizing configured: compact={0}, min={1:0}x{2:0}, max={3:0}x{4:0}, horizontal=true, vertical=true",
                compactMode,
                widget.MinWindowSize.Width,
                widget.MinWindowSize.Height,
                widget.MaxWindowSize.Width,
                widget.MaxWindowSize.Height);
        }

        private void GameBarWidget_VisibleChanged(XboxGameBarWidget sender, object args)
        {
            DebugLog.Write("Game Bar visibility changed: visible={0}", sender.Visible);
            UpdateWebViewMemoryUsageTarget(sender.Visible);
            EnsureWebViewAudioOutputEnabled(MusicWebView.CoreWebView2, "Game Bar visibility change");
            UpdateSystemMediaPlaybackStatus();
        }

        private void GameBarWidget_PinnedChanged(XboxGameBarWidget sender, object args)
        {
            DebugLog.Write("Game Bar pin state changed: pinned={0}", sender.Pinned);
        }

        private void GameBarWidget_WindowBoundsChanged(XboxGameBarWidget sender, object args)
        {
            DebugLog.Write(
                "Game Bar widget bounds changed: {0:0}x{1:0}",
                sender.WindowBounds.Width,
                sender.WindowBounds.Height);
        }

        private void GameBarWidget_DisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            DebugLog.Write("Game Bar display mode changed: {0}", sender.GameBarDisplayMode);
            EnsureWebViewAudioOutputEnabled(MusicWebView.CoreWebView2, "Game Bar display-mode change");
            UpdateSystemMediaPlaybackStatus();
        }

        private async void GameBarWidget_CompactModeEnabledChanged(XboxGameBarWidget sender, object args)
        {
            await Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => ConfigureWidgetSizing(sender));
        }
    }
}
