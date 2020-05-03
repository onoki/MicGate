using MicGate.Pages;
using MicGate.Processing;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MicGate
{
    /// <summary>
    /// Interaction logic for Waves.xaml
    /// </summary>
    public partial class PageWaves : UserControl
    {
        private PageWavesViewModel vm;

        public PageWaves(AudioCore core)
        {
            InitializeComponent();

            vm = new PageWavesViewModel(core);
            DataContext = vm;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            vm.PauseUpdating = true;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            vm.PauseUpdating = false;
        }
    }
}
