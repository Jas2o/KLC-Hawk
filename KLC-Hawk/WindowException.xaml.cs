﻿using System;
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
using System.Windows.Shapes;

namespace KLC_Hawk {
    /// <summary>
    /// Interaction logic for WindowException.xaml
    /// </summary>
    public partial class WindowException : Window {

        public Exception Exception { get; private set; }
        public string ExceptionType { get; private set; }

        public WindowException() {
            InitializeComponent();
        }

        public WindowException(Exception ex, string exType) { //, bool allowContinue
            Exception = ex;
            ExceptionType = exType;
            this.DataContext = this;
            this.Title = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " Exception";

            InitializeComponent();

            //btnContinue.Visibility = (allowContinue ? Visibility.Visible : Visibility.Collapsed);
        }

        private void OnExitAppClick(object sender, RoutedEventArgs e) {
            Environment.Exit(0);
        }

        private void OnContinueAppClick(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
