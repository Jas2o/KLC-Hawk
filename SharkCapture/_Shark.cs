using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class Shark {

        public static readonly byte[] HeaderMagic = new byte[] { 0x4D, 0x49, 0x54, 0x4D }; //MITM
        public static readonly byte HeaderVersion = 0x02; //After one of the ports was removed

        private WindowShark window;
        public bool LoadedFromFile { get; private set; }
        public bool AllowCapture;

        public ObservableCollection<CaptureMsg> ListCapture { get; set; }

        public bool FilterDashboard { get; set; } = true;
        public bool FilterThumbnailResult { get; set; } = true;
        //--
        public bool FilterPing { get; set; } = true;
        public bool FilterFrameAcknowledgement { get; set; } = true;
        public bool FilterVideo { get; set; } = true;
        public bool FilterCursorImage { get; set; } = true;
        public bool FilterMouseMove { get; set; } = true;

        //public BindingList<CaptureMsg> listMessageKLC = new BindingList<CaptureMsg>();
        //private Action<CaptureMsg> actionLog;
        //public AsyncProducerConsumerQueue<CaptureMsg> queueLog;

        public Shark(WindowShark window, bool allowCapture=false) {
            this.window = window;
            ListCapture = new ObservableCollection<CaptureMsg>();

            AllowCapture = allowCapture;
        }

        public void LoadFile(string path) {
            ListCapture.Clear();

            FileStream fs = File.OpenRead(path);
            if (fs.Length > 5) {
                byte[] header = new byte[4];
                fs.Read(header, 0, 4);
                byte version = (byte)fs.ReadByte();

                if (header.SequenceEqual(HeaderMagic) && version == HeaderVersion) {
                    int number = 1;
                    DateTime timeCompare = DateTime.MinValue;
                    while (fs.Position < fs.Length) {
                        CaptureMsg msg = new CaptureMsg(fs, number, timeCompare);
                        ListCapture.Add(msg);

                        if (timeCompare == DateTime.MinValue)
                            timeCompare = msg.Timestamp;
                        number++;
                    }
                }
            }
            fs.Close();

            LoadedFromFile = true;
        }

        DateTime timeCompareNew = DateTime.MinValue;

        public void AddCapture(Side side, int port, string module, ArraySegment<byte> message) {
            CaptureMsg msg = new CaptureMsg(0, timeCompareNew, side, port, module, message);
            if (!msg.FilterHideDefault) {
                window.Dispatcher.Invoke((Action)delegate {
                    msg.Number = ListCapture.Count + 1;
                    ListCapture.Add(msg);
                });
                //queueLog.Produce(msg);
            }

            if (timeCompareNew == DateTime.MinValue)
                timeCompareNew = msg.Timestamp;
        }

        public void AddCapture(Side side, int port, string module, string message) {
            CaptureMsg msg = new CaptureMsg(0, timeCompareNew, side, port, module, message);
            if (!msg.FilterHideDefault) {
                window.Dispatcher.Invoke((Action)delegate {
                    msg.Number = ListCapture.Count + 1;
                    ListCapture.Add(msg);
                });
                //queueLog.Produce(msg);
            }

            if (timeCompareNew == DateTime.MinValue)
                timeCompareNew = msg.Timestamp;
        }

        /*
        MemoryStream msHex;

        private void dgvKLC_SelectionChanged(object sender, EventArgs e) {
            if (dgvKLC.Columns.Count < 7 || dgvKLC.SelectedCells.Count == 0)
                return;

            int number = (int)dgvKLC.SelectedCells[0].OwningRow.Cells[0].Value;
            CaptureMsg msg = listMessageKLC[number - 1];
            msHex = new MemoryStream(msg.Data);
            hexSelected.ByteProvider = new DynamicFileByteProvider(msHex);

            if (msg.Type == Datatype.JSON)
                txtSelected.Text = JsonPrettify(msg.Text);
            else
                txtSelected.Text = msg.Text;

            if (msg.Type == Datatype.Binary) {
                tabControl.SelectedTab = tabHex;
            } else if (msg.Type == Datatype.String) {
                tabControl.SelectedTab = tabText;
            } else if (msg.Type == Datatype.JSON || msg.Type == Datatype.bJSON) {
                tabControl.SelectedTab = tabText;
            } else {
                throw new NotImplementedException();
            }

            dgvKLC.Select();
        }

        private void FormMain_DragDrop(object sender, DragEventArgs e) {
            if (AllowCapture)
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
                LoadFile(files[0]);
        }

        private void toolHexCopy_Click(object sender, EventArgs e) {
            if (msHex == null)
                return;

            Clipboard.SetText(BitConverter.ToString(msHex.ToArray()).Replace("-", ""));
        }

        private void toolFileOpen_Click(object sender, EventArgs e) {
            openFileDialog1.Filter = "KLC-Shark Captures|*.klccap";

            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK) {
                LoadFile(openFileDialog1.FileName);
                LoadedFromFile = true;
                this.Text = "KLC-Shark - " + Path.GetFileName(openFileDialog1.FileName);
            }
        }

        private void toolFileSave_Click(object sender, EventArgs e) {
            saveFileDialog1.FileName = "MITM-" + DateTime.Now.ToString("yyyyMMddTHHmmss") + ".klccap";
            DialogResult result = saveFileDialog1.ShowDialog();
            if (result == DialogResult.OK) {
                FileStream fs = File.Create(saveFileDialog1.FileName);
                fs.Write(HeaderMagic, 0, HeaderMagic.Length);
                fs.WriteByte(HeaderVersion);

                foreach (CaptureMsg msg in listMessageKLC) {
                    byte[] export = msg.ExportAsBytes();
                    fs.Write(export, 0, export.Length);
                }

                fs.Close();

                this.Text = "KLC-Shark - " + Path.GetFileName(saveFileDialog1.FileName);
            }
        }
        */

    }
}
