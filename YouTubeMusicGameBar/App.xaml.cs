using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace YouTubeMusicGameBar
{
    sealed partial class App : Application
    {
        private const string WidgetExtensionId = "YouTubeMusicWidget";
        private XboxGameBarWidget _widget;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            EnteredBackground += OnEnteredBackground;
            LeavingBackground += OnLeavingBackground;
            UnhandledException += OnUnhandledException;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;

            try
            {
                if (args.Kind == ActivationKind.Protocol)
                {
                    IProtocolActivatedEventArgs protocolArgs = args as IProtocolActivatedEventArgs;
                    if (protocolArgs != null &&
                        string.Equals(protocolArgs.Uri.Scheme, "ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
                    {
                        widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                    }
                }

                if (widgetArgs == null)
                {
                    DebugLog.Write("Ignored non-Game-Bar activation.");
                    return;
                }

                DebugLog.Write(
                    "Widget activation: extension={0}, launch={1}",
                    widgetArgs.AppExtensionId,
                    widgetArgs.IsLaunchActivation);

                if (!string.Equals(widgetArgs.AppExtensionId, WidgetExtensionId, StringComparison.Ordinal))
                {
                    ShowActivationError("Xbox Game Bar requested an unknown widget extension.");
                    return;
                }

                if (!widgetArgs.IsLaunchActivation)
                {
                    DebugLog.Write("Repeat widget activation received; preserving the existing widget and WebView.");
                    return;
                }

                Frame rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;

                _widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                if (!rootFrame.Navigate(typeof(MainWidget), _widget))
                {
                    ShowActivationError("The YouTube Music widget page could not be opened.");
                    return;
                }

                Window.Current.Closed += OnWidgetWindowClosed;
                Window.Current.Activate();
                DebugLog.Write("Xbox Game Bar widget created and activated.");
            }
            catch (Exception exception)
            {
                DebugLog.Write("Widget activation failed: {0}", exception);
                ShowActivationError(
                    "YouTube Music could not connect to Xbox Game Bar. Close the widget and open it again.");
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            // A normal app-list identity lets Windows attribute WebView2's media
            // session to this package instead of displaying "Unknown app". Make
            // that Start entry useful by forwarding it to the Game Bar widget.
            string widgetId = Package.Current.Id.FamilyName + "_App_" + WidgetExtensionId;
            Uri activationUri = new Uri("ms-gamebar:/activate/" + widgetId);

            try
            {
                bool launched = await Windows.System.Launcher.LaunchUriAsync(activationUri);
                DebugLog.Write("Start-menu launch forwarded to Game Bar: {0}", launched);
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not forward Start-menu launch to Game Bar: {0}", exception);
            }
        }

        private void ShowActivationError(string message)
        {
            try
            {
                Frame rootFrame = Window.Current.Content as Frame;
                if (rootFrame == null)
                {
                    rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                }

                rootFrame.Navigate(typeof(ActivationErrorPage), message);
                Window.Current.Activate();
            }
            catch (Exception exception)
            {
                DebugLog.Write("Could not display activation error: {0}", exception);
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs args)
        {
            DebugLog.Write("XAML navigation failed for {0}: {1}", args.SourcePageType, args.Exception);
            args.Handled = true;
            ShowActivationError("The widget interface could not be loaded. Close the widget and try again.");
        }

        private void OnWidgetWindowClosed(object sender, Windows.UI.Core.CoreWindowEventArgs args)
        {
            DebugLog.Write("Game Bar widget window closed.");
            Window.Current.Closed -= OnWidgetWindowClosed;

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                MainWidget widgetPage = rootFrame.Content as MainWidget;
                if (widgetPage != null)
                {
                    widgetPage.ReleaseResources("widget window close");
                }

                rootFrame.NavigationFailed -= OnNavigationFailed;
            }

            _widget = null;
        }

        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            // Keep the widget reference intact. Background-media playback should
            // prevent suspension while audio is active, and clearing this object
            // would sever Game Bar state if Windows later resumes the same process.
            DebugLog.Write("Application suspending while no background audio is active.");
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs args)
        {
            DebugLog.Write("Application entered background; background-media mode remains active.");
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs args)
        {
            DebugLog.Write("Application returned to foreground.");
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            DebugLog.Write("Unhandled UI exception: {0}", args.Exception);
        }
    }
}
