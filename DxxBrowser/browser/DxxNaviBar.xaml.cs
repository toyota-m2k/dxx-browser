using Common;
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

namespace DxxBrowser {
    /// <summary>
    /// DxxBrowserView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxNaviBar : UserControl {
        public DxxWebViewHost ViewModel {
            get => DataContext as DxxWebViewHost;
            set => DataContext = value;
        }

        public DxxNaviBar() {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            var edit = Utils.FindChild(urlInput, "PART_EditableTextBox", typeof(TextBox)) as TextBox;
            if(null!=edit) {
                var th = edit.Margin;
                th.Right = 25;
                edit.Margin = th;
            }
        }
    }
}
