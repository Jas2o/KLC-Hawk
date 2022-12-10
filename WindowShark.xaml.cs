using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    /// <summary>
    /// Interaction logic for WindowShark.xaml
    /// </summary>
    public partial class WindowShark : Window {

        public Shark Shark;
        //private MemoryStream msHex;

        public WindowShark(bool allowCapture = false) {
            Shark = new Shark(this, allowCapture);
            this.DataContext = Shark;
            InitializeComponent();

            if (Shark.AllowCapture)
                this.Title = "KLC-Shark - Capturing";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (!Shark.AllowCapture)
                menuFileOpen_Click(sender, e);
        }

        private void menuFileOpen_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "KLC-Shark Captures|*.klccap";

            bool result = (bool)openFileDialog.ShowDialog();
            if (result) {
                Shark.LoadFile(openFileDialog.FileName);
                this.Title = "KLC-Shark - " + System.IO.Path.GetFileName(openFileDialog.FileName);
            }
        }

        private void menuFileSave_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = "MITM-" + DateTime.Now.ToString("yyyyMMddTHHmmss") + ".klccap";
            bool result = (bool)saveFileDialog.ShowDialog();
            if (result) {
                FileStream fs = File.Create(saveFileDialog.FileName);
                fs.Write(Shark.HeaderMagic, 0, Shark.HeaderMagic.Length);
                fs.WriteByte(Shark.HeaderVersion);

                foreach (CaptureMsg msg in Shark.ListCapture) {
                    byte[] export = msg.ExportAsBytes();
                    fs.Write(export, 0, export.Length);
                }

                fs.Close();

                this.Title = "KLC-Shark - " + System.IO.Path.GetFileName(saveFileDialog.FileName);
            }
        }

        private void menuCapturePause_Click(object sender, RoutedEventArgs e) {
            if (Shark.LoadedFromFile)
                return;

            Shark.AllowCapture = false;
            this.Title = "KLC-Shark - Paused";
        }

        private void menuCaptureContinue_Click(object sender, RoutedEventArgs e) {
            if (Shark.LoadedFromFile)
                return;

            Shark.AllowCapture = true;
            this.Title = "KLC-Shark - Capturing";
        }

        private void menuHexCopy_Click(object sender, RoutedEventArgs e) {
            //Was never implemented
        }

        public static string JsonPrettify(string json) {
            using (var stringReader = new StringReader(json))
            using (var stringWriter = new StringWriter()) {
                var jsonReader = new JsonTextReader(stringReader);
                var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
                jsonWriter.WriteToken(jsonReader);
                return stringWriter.ToString();
            }
        }

        private void dataGridCapture_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            CaptureMsg msg = (CaptureMsg)dataGridCapture.SelectedValue;
            if (msg == null)
                return;

            txtHex.Text = BitConverter.ToString(msg.Data).Replace("-", "");
            //msHex = new MemoryStream(msg.Data);
            //hexSelected.ByteProvider = new DynamicFileByteProvider(msHex);

            if (msg.Type == Datatype.JSON)
                txtSelected.Text = JsonPrettify(msg.Text);
            else
                txtSelected.Text = msg.Text;

            if (msg.Type == Datatype.Binary) {
                tabControl.SelectedItem = tabHex;
            } else if (msg.Type == Datatype.String) {
                tabControl.SelectedItem = tabText;
            } else if (msg.Type == Datatype.JSON || msg.Type == Datatype.bJSON) {
                tabControl.SelectedItem = tabText;
            } else {
                throw new NotImplementedException();
            }
        }

        private void menuFilterRefresh_Click(object sender, RoutedEventArgs e)
        {
            List<string> filters = new List<string>();
            if (Shark.FilterDashboard) filters.Add("Dashboard");
            if (Shark.FilterThumbnailResult) filters.Add("ThumbnailResult");
            if (Shark.FilterPing) filters.Add("Ping");
            if (Shark.FilterFrameAcknowledgement) filters.Add("FrameAcknowledgement");
            if (Shark.FilterVideo) filters.Add("Video");
            if (Shark.FilterCursorImage) filters.Add("CursorImage");
            if (Shark.FilterMouseMove) filters.Add("MouseMove");

            ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dataGridCapture.ItemsSource);
            collectionView.Filter = new Predicate<object>(x =>
                //!((CaptureMsg)x).FilterHideDefault
                !filters.Contains(((CaptureMsg)x).FilterReason)
            );
        }
    }
}
