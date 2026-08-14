using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace YouTubeMusicGameBar
{
    public sealed partial class ActivationErrorPage : Page
    {
        public ActivationErrorPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs args)
        {
            base.OnNavigatedTo(args);
            MessageTextBlock.Text = args.Parameter as string ??
                "Close this widget and open it again from Xbox Game Bar.";
        }
    }
}
