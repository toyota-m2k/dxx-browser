using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DxxBrowser {
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            DxxGlobal.Initialize();
        }
        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);
            DxxGlobal.Terminate();
        }
    }
}
