using Microsoft.Toolkit.Wpf.UI.Controls;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace DxxBrowser {
    /// <summary>
    /// DxxBrowserView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxBrowserView : UserControl, DxxWebViewManager.IDxxWebViewContainer {

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
            DxxWebViewManager.Instance.PrepareBrowser(this);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            naviBar.ViewModel.Dispose();
        }

        #pragma warning disable CS0618 // 型またはメンバーが古い形式です

        public void AttachWebView(WebView wv) {
            browserHostGrid.Children.Add(wv);
            naviBar.ViewModel.SetBrowser(wv);
        }

        public WebView DetachWebView() {
            if (browserHostGrid.Children.Count > 0) {
                var r = browserHostGrid.Children[0] as WebView;
                browserHostGrid.Children.Clear();
                naviBar.ViewModel.ResetBrowser();
                return r;
            }
            return null;
        }

        #pragma warning restore CS0618 // 型またはメンバーが古い形式です
    }
}
