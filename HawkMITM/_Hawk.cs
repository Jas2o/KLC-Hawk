using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static LibKaseya.Enums;

namespace KLC_Hawk {

    public class Hawk {

        private WindowMain windowMain;

        private NamedPipeListener<String> pipeListener;
        private LiveConnectSession lastSession;
        private bool useHalfMode;

        //private FormShark formSharkCapture;
        //private FormOverlay formOverlay;

        private Action<string> actionLog;
        public AsyncProducerConsumerQueue<string> queueLog;

        public bool LogClipboardEvents;
        public bool LogHacksKeyRelease;
        public bool EnableKeyboardReleaseHack;// { get; private set; }
        public bool EnableClipboardHostToRemote { get; private set; }
        public bool EnablePrintScreenHack;// { get; private set; }
        public bool EnableFrameAckHack;// { get; private set; }
        public bool EnableAutoType;// { get; private set; }
        public int AutoTypeSpeed;// { get; private set; }

        public Hawk(WindowMain window) {
            windowMain = window;
            EnableFrameAckHack = true;
            EnableAutoType = true;
            EnableKeyboardReleaseHack = true;

            actionLog = new Action<string>((x) => {
                windowMain.Dispatcher.Invoke((Action)delegate {
                    windowMain.txtLog.AppendText(x + "\r\n");

                    if (windowMain.IsActive) {
                        windowMain.txtLog.Focus();
                        windowMain.txtLog.CaretIndex = windowMain.txtLog.Text.Length;
                        windowMain.txtLog.ScrollToEnd();
                    }
                });
            });
            queueLog = new AsyncProducerConsumerQueue<string>(actionLog);

            pipeListener = new NamedPipeListener<String>("KLCMITM");
            pipeListener.MessageReceived += PipeListener_MessageReceived;
            pipeListener.Error += PipeListener_Error;
            pipeListener.Start();

            //--

            //toolHacksKeyRelease.Checked = EnableKeyboardReleaseHack = true;
            //toolHacksPrintScreen.Checked = EnablePrintScreenHack = true;
            //toolHacksFrameAck.Checked = EnableFrameAckHack = true;
            //toolAutotype.PerformClick();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 2) {
                lastSession = LiveConnectSession.Create(int.Parse(args[2]), this);
            } else {
            }
        }

        private void PipeListener_MessageReceived(object sender, NamedPipeListenerMessageReceivedEventArgs<string> e) {
            lastSession = LiveConnectSession.Create(int.Parse(e.Message), this, useHalfMode);
        }

        private void PipeListener_Error(object sender, NamedPipeListenerErrorEventArgs e) {
            string error = string.Format("Pipe Error ({0}): {1}", e.ErrorType, e.Exception.ToString());
            LogText(error);
        }

        public void LogText(string message, string filterable = "") {
            if (filterable == "autotype" && !LogClipboardEvents)
                return;
            if (filterable == "clipboard" && !LogClipboardEvents)
                return;
            if (filterable == "keyrelease" && !LogHacksKeyRelease)
                return;

            if (filterable == "screenshot") {
                //throw new NotImplementedException("There should be a notificiation that the screenshot got captured");
                //Also the 'slow screenshot' overlay should appear if fast print screen isn't enabled

                //notifyIcon1.BalloonTipTitle = (EnablePrintScreenHack ? "Fast" : "Slow") + " Screenshot";
                //return;
            }

            queueLog.Produce(message);
        }

        public bool ToggleClipboardSync() {
            EnableClipboardHostToRemote = !EnableClipboardHostToRemote;
            return EnableClipboardHostToRemote;
        }

        public void LogOld(Side side, int port, string module, byte[] message) {
            if (windowMain.WindowSharkCapture == null || !windowMain.WindowSharkCapture.IsVisible || !windowMain.WindowSharkCapture.Shark.AllowCapture)
                return;

            windowMain.WindowSharkCapture.Shark.AddCapture(side, port, module, message);
        }

        public void LogOld(Side side, int port, string module, string message) {
            if (windowMain.WindowSharkCapture == null || !windowMain.WindowSharkCapture.IsVisible || !windowMain.WindowSharkCapture.Shark.AllowCapture)
            return;

            windowMain.WindowSharkCapture.Shark.AddCapture(side, port, module, message);
        }

        /*
        public void ActivateOverlay(WsB websocketB, string client) {
            Invoke(new MethodInvoker(() => {

                if (formOverlay != null && formOverlay.Visible)
                    formOverlay.Close();

                formOverlay = new FormOverlay(websocketB, client);

                formOverlay.Show();
            }));
        }
        */

        public string GetWiresharkFiltersAll() {
            if (lastSession == null)
                return "";
            return LiveConnectSession.GetWiresharkFilter(lastSession);
        }

        public string GetWiresharkFiltersLiveConnect() {
            if (lastSession == null)
                return "";
            return lastSession.GetWiresharkFilterKLC();
        }

        public string GetWiresharkFiltersAdminEndPoint() {
            if (lastSession == null)
                return "";
            return lastSession.GetWiresharkFilterAEP();
        }

        public void ToggleHalfMode() {
            //This intentionally breaks MITM, only useful for wireshark capture without proxying everything.
            useHalfMode = !useHalfMode;
            LogText("Half mode " + (useHalfMode ? "enabled" : "disabled") + " for next connection.");
        }

        /*
        private void toolAutotype_Click(object sender, EventArgs e) {
            if (toolAutotypeSpeed.SelectedIndex == -1)
                toolAutotypeSpeed.SelectedIndex = 0;
            EnableAutoType = !EnableAutoType;

            toolAutotypeSpeed.Visible = EnableAutoType;
        }

        private void toolAutotypeSpeed_SelectedIndexChanged(object sender, EventArgs e) {
            AutoTypeSpeed = toolAutotypeSpeed.SelectedIndex;
        }

        private void toolClipboardSync_Click(object sender, EventArgs e) {
            EnableClipboardHostToRemote = !EnableClipboardHostToRemote;

            toolClipboardSync.Text = "Clipboard (" + (EnableClipboardHostToRemote ? "Synced" : "Receive Only") + ")";
        }

        private void toolLogClipboard_Click(object sender, EventArgs e) {
            LogClipboardEvents = toolLogClipboard.Checked = !toolLogClipboard.Checked;
        }

        private void toolLogHacksKeyRelease_Click(object sender, EventArgs e) {
            LogHacksKeyRelease = toolLogHacksKeyRelease.Checked = !toolLogHacksKeyRelease.Checked;
        }

        private void toolHacksPrintScreen_Click(object sender, EventArgs e) {
            EnablePrintScreenHack = toolHacksPrintScreen.Checked = !toolHacksPrintScreen.Checked;
        }

        private void toolHacksFrameAck_Click(object sender, EventArgs e) {
            EnableFrameAckHack = toolHacksFrameAck.Checked = !toolHacksFrameAck.Checked;
        }

        private void toolHacksKeyRelease_Click(object sender, EventArgs e) {
            EnableKeyboardReleaseHack = toolHacksKeyRelease.Checked = !toolHacksKeyRelease.Checked;
        }
        */

    }
}
