using MicGate.Pages;
using MicGate.Processing;
using System.Windows.Controls;

namespace MicGate
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class PageSettings : UserControl
    {
        private PageSettingsViewModel vm;

        public PageSettings(AudioCore core)
        {
            InitializeComponent();

            vm = new PageSettingsViewModel(core);
            DataContext = vm;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }
    }
}
