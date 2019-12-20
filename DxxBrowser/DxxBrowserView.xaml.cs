using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class DxxBrowserView : UserControl {

        public DxxWebViewHost ViewModel {
            get => DataContext as DxxWebViewHost;
            set => DataContext = value;
        }

        public DxxBrowserView() {
            InitializeComponent();
        }

        public void Initialize(bool isMain, DxxBookmark bookmarks,
            ReactiveProperty<ObservableCollection<DxxTargetInfo>> targetList) {
            var vm = new DxxWebViewHost(webView, isMain, Window.GetWindow(this), bookmarks, targetList);
            naviBar.ViewModel = vm;
            this.ViewModel = vm;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            naviBar.ViewModel.Dispose();
        }
    }
}
