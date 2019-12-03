using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DxxBrowser {
    public enum DxxNaviMode {
        Self,
        SubView,
        Listing,
    }

    public class DxxMainViewModel : DxxViewModelBase, IDisposable {
        public ReactiveProperty<DxxNaviMode> NaviMode { get; } = new ReactiveProperty<DxxNaviMode>(DxxNaviMode.Self);
        public ReactiveProperty<string> TargetUrl { get; } = new ReactiveProperty<string>();
        public ReactiveCommand<string> NavigateCommand { get; } = new ReactiveCommand<string>();

        public ReactiveProperty<bool> Loaded { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);

        public delegate void NavigateToProc(string url);
        public event NavigateToProc NavigateTo;

        public DxxMainViewModel() {
            NavigateCommand.Subscribe((v) => {
                NavigateTo?.Invoke(v);
            });
        }

        public void Dispose() {
            NaviMode.Dispose();
            TargetUrl.Dispose();
            NavigateCommand.Dispose();
            NavigateTo = null;
        }

    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxMainWindow : Window {
        public DxxMainViewModel ViewModel {
            get => DataContext as DxxMainViewModel;
            private set { DataContext = value; }
        }

        private bool mLoadingMain = false;

        private List<IDxxDriver> mDrivers;


        public DxxMainWindow() {
            InitializeDrivers();
            InitializeComponent();
        }


        private void NavigateTo(string url) {
            if (mainBrowser.Source.ToString() != url) {
                mLoadingMain = true;
                mainBrowser.Navigate(url);
            }
        }

        private void OnHistoryPrev(object sender, RoutedEventArgs e) {
            mLoadingMain = true;
            mainBrowser.GoBack();
        }

        private void OnHistoryNext(object sender, RoutedEventArgs e) {
            mLoadingMain = true;
            mainBrowser.GoForward();
        }

        private void OnReload(object sender, RoutedEventArgs e) {
            mLoadingMain = true;
            mainBrowser.Refresh();
        }


        private void OnLoaded(object sender, RoutedEventArgs e) {
            ViewModel = new DxxMainViewModel();
            ViewModel.NavigateTo += NavigateTo;
            mainBrowser.NavigationStarting += WebView_NavigationStarting;
            mainBrowser.NavigationCompleted += WebView_NavigationCompleted;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
            mainBrowser.NavigationStarting -= WebView_NavigationStarting;
            mainBrowser.NavigationCompleted -= WebView_NavigationCompleted;
        }

        private void WebView_NavigationStarting(object sender, WebViewControlNavigationStartingEventArgs e) {
            if (!mLoadingMain) {
                switch (ViewModel.NaviMode.Value) {
                    case DxxNaviMode.Listing:
                        e.Cancel = true;
                        break;
                    case DxxNaviMode.SubView:
                        e.Cancel = true;
                        subBrowser.Navigate(e.Uri);
                        break;
                    default:
                        ViewModel.Loaded.Value = false;
                        break;
                }
                ViewModel.TargetUrl.Value = e.Uri.ToString();
            }
        }

        private void WebView_NavigationCompleted(object sender, WebViewControlNavigationCompletedEventArgs e) {
            mLoadingMain = false;
            ViewModel.TargetUrl.Value = e.Uri.ToString();
            ViewModel.Loaded.Value = true;
            ViewModel.HasPrev.Value = mainBrowser.CanGoBack;
            ViewModel.HasNext.Value = mainBrowser.CanGoForward;
        }
    }
}
