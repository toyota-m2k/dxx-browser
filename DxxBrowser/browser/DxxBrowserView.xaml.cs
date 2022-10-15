using Reactive.Bindings;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

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
            var vm = new DxxWebViewHost(isMain, Window.GetWindow(this), bookmarks, targetList);
            naviBar.ViewModel = vm;
            this.ViewModel = vm;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            naviBar.ViewModel.Dispose();
            ViewModel.ResetBrowser();
        }


        private void WV2CoreWebView2InitializationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e) {
            ViewModel.SetBrowser(webView);
        }

    }
}
