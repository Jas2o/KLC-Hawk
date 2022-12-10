using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

/*
cd C:\Users\username\source\repos\KLC-Dev\KLC-Hawk\bin\Debug\net6.0-windows

//LOCAL APP DATA
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe" "KLC-Hawk.exe"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\KLC-Hawk.dll" "KLC-Hawk.dll"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\WatsonWebsocket.dll" "WatsonWebsocket.dll"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\Fleck.dll" "Fleck.dll"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\Newtonsoft.Json.dll" "Newtonsoft.Json.dll"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\RestSharp.dll" "RestSharp.dll"
mklink /H "%localappdata%\Apps\Kaseya Live Connect-MITM\VP8.NET.dll" "VP8.NET.dll"
//And the others

//PROGRAM FILES
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe" "KLC-Hawk.exe"
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\WatsonWebsocket.dll" "WatsonWebsocket.dll"
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\Fleck.dll" "Fleck.dll"
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\Newtonsoft.Json.dll" "Newtonsoft.Json.dll"
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\RestSharp.dll" "RestSharp.dll"
mklink /H "C:\Program Files\Kaseya Live Connect-MITM\VP8.NET.dll" "VP8.NET.dll"
//And the others
*/

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
                    //if(false)
                        //ShowUnhandledExceptionFromSrc(args.Exception, "TaskScheduler.UnobservedTaskException");
                    args.SetObserved();
                };

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    //if(false)
                        //ShowUnhandledExceptionFromSrc(args.Exception, "Dispatcher.UnhandledException");
                };
            }

            mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew) {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length > 2) { //Console=1, Window=2
                    NamedPipeListener.SendMessage("KLCMITM", true, args[2]);
                }

                App.Current.Shutdown();
            }
        }

        public static void ShowUnhandledExceptionFromSrc(Exception e, string source) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                new WindowException(e, source + " - " + e.GetType().ToString()).Show();
            });
        }

        public static void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //, Debugger.IsAttached
        }

    }
}
