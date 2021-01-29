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
                TaskScheduler.UnobservedTaskException += (sender, args) => ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
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

        void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //, Debugger.IsAttached
        }

    }
}
