using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KLC_Hawk {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class WindowMain : Window {

        private Hawk Hawk;

        public WindowShark WindowSharkCapture;
        private WindowOverlay windowOverlayTest;

        public WindowMain() {
            Hawk = new Hawk(this);
            this.DataContext = Hawk;
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Environment.Exit(0);
        }

        #region Log
        private void menuLogClear_Click(object sender, RoutedEventArgs e) {
            txtLog.Clear();
        }
        #endregion

        #region Capture
        private void menuStartCapture_Click(object sender, RoutedEventArgs e) {
            if (WindowSharkCapture != null && WindowSharkCapture.IsVisible)
                return;

            WindowSharkCapture = new WindowShark(true);
            WindowSharkCapture.Show();
        }

        private void menuOpenCapture_Click(object sender, RoutedEventArgs e) {
            new WindowShark().Show();
        }

        private void menuFilterAll_Click(object sender, RoutedEventArgs e) {
            string filter = Hawk.GetWiresharkFiltersAll();
            if (filter != "")
                Clipboard.SetText(filter);
        }

        private void menuFilterLC_Click(object sender, RoutedEventArgs e) {
            string filter = Hawk.GetWiresharkFiltersLiveConnect();
            if (filter != "")
                Clipboard.SetText(filter);
        }

        private void menuFilterAEP_Click(object sender, RoutedEventArgs e) {
            string filter = Hawk.GetWiresharkFiltersAdminEndPoint();
            if (filter != "")
                Clipboard.SetText(filter);
        }
        #endregion

        #region Autotype
        private void menuAutotypeFast_Click(object sender, RoutedEventArgs e) {
            Hawk.AutoTypeSpeed = 0;
            menuAutotypeFast.IsChecked = true;
            menuAutotypeAverage.IsChecked = false;
            menuAutotypeSlow.IsChecked = false;
        }

        private void menuAutotypeAverage_Click(object sender, RoutedEventArgs e) {
            Hawk.AutoTypeSpeed = 1;
            menuAutotypeFast.IsChecked = false;
            menuAutotypeAverage.IsChecked = true;
            menuAutotypeSlow.IsChecked = false;
        }

        private void menuAutotypeSlow_Click(object sender, RoutedEventArgs e) {
            Hawk.AutoTypeSpeed = 2;
            menuAutotypeFast.IsChecked = false;
            menuAutotypeAverage.IsChecked = false;
            menuAutotypeSlow.IsChecked = true;
        }

        private void menuAutotypeDisable_Click(object sender, RoutedEventArgs e) {
            Hawk.EnableAutoType = menuAutotypeDisable.IsChecked;
        }
        #endregion

        #region Clipboard
        private void toolClipboardSync_Click(object sender, RoutedEventArgs e) {
            bool cbsync = Hawk.ToggleClipboardSync();
            //toolClipboardSync.Overflow = (clipboardSyncEnabled ? ToolStripItemOverflow.AsNeeded : ToolStripItemOverflow.Always);

            if (cbsync)
                toolClipboardSync.Header = "Clipboard (Synced)";
            else
                toolClipboardSync.Header = "Clipboard (Receive Only)";
        }
        #endregion

        #region Hacks
        private void menuHacksKeyRelease_Click(object sender, RoutedEventArgs e) {
            Hawk.EnableKeyboardReleaseHack = menuHacksKeyRelease.IsChecked;
        }

        private void menuHacksFastPrintScreen_Click(object sender, RoutedEventArgs e) {
            Hawk.EnablePrintScreenHack = menuHacksFastPrintScreen.IsChecked;
        }

        private void menuHacksEarlyFrameAck_Click(object sender, RoutedEventArgs e) {
            Hawk.EnableFrameAckHack = menuHacksEarlyFrameAck.IsChecked;
        }
        #endregion

        #region Test
        private void menuTestOverlay_Click(object sender, RoutedEventArgs e) {
            if (windowOverlayTest != null && windowOverlayTest.IsVisible)
                return;

            windowOverlayTest = new WindowOverlay();
            windowOverlayTest.Show();
        }
        #endregion

        private void menuFilterHalf_Click(object sender, RoutedEventArgs e) {
            Hawk.ToggleHalfMode();
        }

        private void menuDropA_Click(object sender, RoutedEventArgs e)
        {
            Hawk.DropA();
        }

        private void menuDropB_Click(object sender, RoutedEventArgs e)
        {
            Hawk.DropB();
        }

        private void menuDropY_Click(object sender, RoutedEventArgs e)
        {
            Hawk.DropY();
        }

        private void menuDropZ_Click(object sender, RoutedEventArgs e)
        {
            Hawk.DropZ();
        }
    }
}
