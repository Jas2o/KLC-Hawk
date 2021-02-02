using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

//cd C:\Program Files\Kaseya Live Connect-MITM\
//mklink /H Kaseya.AdminEndpoint.exe "\EIT\Code\KaseyaAlternative\KLC-Hawk\bin\Debug\KLC-Hawk.exe"
//mklink /H WatsonWebsocket.dll "\EIT\Code\KaseyaAlternative\KLC-Hawk\bin\Debug\WatsonWebsocket.dll"
//And the others

namespace KLC_Hawk {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        
        private const string appName = "MITMAdminEndpoint";
        private static Mutex mutex = null;

        public App() : base() {
            if (!Debugger.IsAttached) {
                //Setup exception handling rather than closing rudely.
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                TaskScheduler.UnobservedTaskException += (sender, args) => {
                    ShowUnhandledExceptionFromSrc(args.Exception, "TaskScheduler.UnobservedTaskException");
                    args.SetObserved();
                };

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    ShowUnhandledExceptionFromSrc(args.Exception, "Dispatcher.UnhandledException");
                };
            }

            bool createdNew;
            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew) {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length > 2) { //Console=1, Window=2
                    NamedPipeListener<String>.SendMessage("KLCMITM", args[2]);
                }

                App.Current.Shutdown();
            }
        }

        public static void ShowUnhandledExceptionFromSrc(Exception e, string source) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                new WindowException(e, source + " - " + e.GetType().ToString()).Show();
            });
        }

        void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //, Debugger.IsAttached
        }

    }
}
