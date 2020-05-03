using MahApps.Metro.Controls;
using MicGate.Processing;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MicGate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Main : MetroWindow
    {
        private AudioCore audioCore;
        private bool closeInsteadOfMinimize = false;
        Mutex singleInstanceMutex;

        // Frame element keeps pages in memory if navigated through objects (not URIs).
        // Thus it is better to save the references than create always new ones.
        private PageWaves pageWaves;
        private PageSettings pageSettings;
        private PageAbout pageAbout;
        private UserControl pageCurrent;

        public Main()
        {
            InitializeComponent();

            audioCore = new AudioCore();
            BtnPageWaves_Click(BtnPageWaves, null);

            // allow only one instance of MicGate to run. Limit based on assembly GUID
            var guid = ((GuidAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(GuidAttribute), false)).Value;
            singleInstanceMutex = new Mutex(true, guid, out var isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("An instance of MicGate is already running. The newly opened instance will be closed now.");
                closeInsteadOfMinimize = true;
                Application.Current.Shutdown();
            }

            // minimizing has more steps than just setting WindowState. Thus use the functions.
            if (Utility.StrToBool(Utility.ReadSetting("StartWindowMinimized")))
            {
                Window_Closing(null, null);
            }
            else
            {
                TrayIconOpen_Click(null, null);
            }
        }

        private void UpdateAllButtonStyles(Button activeTab)
        {
            var selectedTabColor = Brushes.Red;
            var unselectedTabColor = Brushes.White;

            BtnPageWaves.Foreground = activeTab == BtnPageWaves ? selectedTabColor : unselectedTabColor;
            BtnPageSettings.Foreground = activeTab == BtnPageSettings ? selectedTabColor : unselectedTabColor;
            BtnPageAbout.Foreground = activeTab == BtnPageAbout ? selectedTabColor : unselectedTabColor;
        }

        private void BtnPageWaves_Click(object sender, RoutedEventArgs e)
        {
            UpdateAllButtonStyles((Button)sender);
            if (pageWaves == null) pageWaves = new PageWaves(audioCore);
            pageCurrent = pageWaves;
            PageNavigator.Navigate(pageWaves);
        }

        private void BtnPageSettings_Click(object sender, RoutedEventArgs e)
        {
            UpdateAllButtonStyles((Button)sender);
            if (pageSettings == null) pageSettings = new PageSettings(audioCore);
            pageCurrent = pageSettings;
            PageNavigator.Navigate(pageSettings);
        }

        private void BtnPageAbout_Click(object sender, RoutedEventArgs e)
        {
            UpdateAllButtonStyles((Button)sender);
            if (pageAbout == null) pageAbout = new PageAbout();
            pageCurrent = pageAbout;
            PageNavigator.Navigate(pageAbout);
        }

        private void TrayIconOpen_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            PageNavigator.Navigate(pageCurrent);
        }

        private void TrayIconClose_Click(object sender, RoutedEventArgs e)
        {
            closeInsteadOfMinimize = true;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!closeInsteadOfMinimize)
            {
                WindowState = WindowState.Minimized;
                PageNavigator.Navigate(null);
                ShowInTaskbar = false;
                if (e != null) e.Cancel = true;
            }
            else
            {
                audioCore.Shutdown();
                TrayIcon.Dispose();
            }
        }
    }
}
