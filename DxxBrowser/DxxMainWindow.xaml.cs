using DxxBrowser.driver;
using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DxxBrowser {
    public enum DxxNaviMode {
        Self,
        SubView,
        DirectDL,
    }

    public class DxxMainViewModel : DxxViewModelBase, IDisposable {
        public ReactiveProperty<DxxNaviMode> NaviMode { get; } = new ReactiveProperty<DxxNaviMode>(DxxNaviMode.Self);
        public ReactiveProperty<string> MainUrl { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> SubUrl { get; } = new ReactiveProperty<string>();
        public ReactiveCommand<string> NavigateCommand { get; } = new ReactiveCommand<string>();

        public ReactiveProperty<bool> Loaded { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);

        public delegate void NavigateToProc(string url);
        public event NavigateToProc NavigateTo;

        public ReactiveProperty<string> DriverName { get; } = new ReactiveProperty<string>(DxxDriverManager.DEFAULT.Name);
        public ReactiveProperty<bool> SettingEnabled { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> IsTarget { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainerList { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> SubViewDownloadable { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> IsDownloading { get; } = DxxDownloader.Instance.Busy.ToReactiveProperty();

        public ReactiveProperty<ObservableCollection<DxxTargetInfo>> TargetList { get; } = new ReactiveProperty<ObservableCollection<DxxTargetInfo>>(new ObservableCollection<DxxTargetInfo>());
        public ReactiveProperty<bool> ShowTargetList { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<ObservableCollection<DxxDownloadingItem>> DownloadingList { get; } = new ReactiveProperty<ObservableCollection<DxxDownloadingItem>>(DxxDownloader.Instance.DownloadingStateList);

        private DxxUrl mDxxSubUrl = null;
        private DxxUrl mDxxUrl = null;
        private IDxxDriver mDriver = DxxDriverManager.DEFAULT;

        public IDxxDriver Driver {
            get => mDriver;
            set {
                if(mDriver!=value) {
                    mDriver = value;
                    DriverName.Value = value.Name;
                    SettingEnabled.Value = mDriver.HasSettings;
                }
            }
        }

        public DxxUrl DxxUrl => mDxxUrl;
        public DxxUrl DxxSubUrl => mDxxSubUrl;

        public DxxMainViewModel() {
            NavigateCommand.Subscribe((v) => {
                NavigateTo?.Invoke(v);
            });
            MainUrl.Subscribe((v) => {
                var driver = DxxDriverManager.Instance.FindDriver(v);
                if(driver!=null) {
                    mDxxUrl = new DxxUrl(new Uri(v), driver, "");
                    Driver = driver;
                } else {
                    mDxxUrl = null;
                    Driver = DxxDriverManager.DEFAULT;
                }
                UpdateContent();
            });
            SubUrl.Subscribe((v) => {
                if (Driver.IsSupported(v)) { 
                    mDxxSubUrl = new DxxUrl(new Uri(v), Driver, "");
                    SubViewDownloadable.Value = mDxxSubUrl.IsContainer || mDxxSubUrl.IsTarget;
                } else {
                    mDxxSubUrl = null;
                    SubViewDownloadable.Value = false;
                }
            });
        }

        private void UpdateContent() {
            if(mDxxUrl==null) {
                IsTarget.Value = false;
                IsContainer.Value = false;
                IsContainerList.Value = false;
            } else {
                IsTarget.Value = Driver.LinkExtractor.IsTarget(mDxxUrl.Uri);
                IsContainer.Value = Driver.LinkExtractor.IsContainer(mDxxUrl.Uri);
                IsContainerList.Value = Driver.LinkExtractor.IsContainerList(mDxxUrl.Uri);
            }
        }
        public void Dispose() {
            NaviMode.Dispose();
            MainUrl.Dispose();
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

        public DxxMainWindow() {
            InitializeComponent();
        }

        private void NavigateTo(string url) {
            var uri = DxxUrl.FixUpUrl(url);
            if (mainBrowser.Source.ToString() != url.ToString()) {
                mLoadingMain = true;
                try {
                    mainBrowser.Navigate(uri.ToString());
                } catch (Exception) {
                    mLoadingMain = false;
                }
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
                if (ViewModel.IsContainerList.Value) {
                    switch (ViewModel.NaviMode.Value) {
                        case DxxNaviMode.DirectDL:
                            var driver = DxxDriverManager.Instance.FindDriver(e.Uri.ToString());
                            if(driver!=null) {
                                var du = new DxxUrl(e.Uri, driver, "");
                                du.Download();
                            }
                            e.Cancel = true;
                            break;
                            
                        case DxxNaviMode.SubView:
                            e.Cancel = true;
                            subBrowser.Navigate(e.Uri);
                            ViewModel.SubUrl.Value = e.Uri.ToString();
                            break;
                        default:
                            break;
                    }
                }
            }
            ViewModel.Loaded.Value = false;
            ViewModel.MainUrl.Value = e.Uri.ToString();
        }

        private void WebView_NavigationCompleted(object sender, WebViewControlNavigationCompletedEventArgs e) {
            mLoadingMain = false;
            ViewModel.MainUrl.Value = e.Uri.ToString();
            ViewModel.Loaded.Value = true;
            ViewModel.HasPrev.Value = mainBrowser.CanGoBack;
            ViewModel.HasNext.Value = mainBrowser.CanGoForward;
        }

        private void DownloadTarget(object sender, RoutedEventArgs e) {
            ViewModel.DxxUrl.Download();
        }

        private async void DownloadAllTarget(object sender, RoutedEventArgs e) {
            var targets = await ViewModel.DxxUrl.TryGetTargetContainers();
            if (targets.Count > 0) {
                ViewModel.DxxUrl.DownloadTargets(targets);
            }
        }

        private async void ShowTargetList(object sender, RoutedEventArgs e) {
            var targets = await ViewModel.DxxUrl.TryGetTargetContainers();
            if (targets.Count > 0) {
                ViewModel.TargetList.Value = new ObservableCollection<DxxTargetInfo>(targets);
                ViewModel.ShowTargetList.Value = true;
            } else {
                ViewModel.TargetList.Value?.Clear();
                ViewModel.ShowTargetList.Value = false;
            }
        }

        private void DownloadFromSubView(object sender, RoutedEventArgs e) {
            ViewModel.DxxSubUrl?.Download();
        }

        private void ClearDownloadingList(object sender, RoutedEventArgs e) {
            ViewModel.DownloadingList.Value.Clear();
        }

        private void StopDownloading(object sender, RoutedEventArgs e) {
            DxxDownloader.Instance.CancelAllAsync();
        }

        private void CloseTargetList(object sender, RoutedEventArgs e) {
            ViewModel.TargetList.Value?.Clear();
            ViewModel.ShowTargetList.Value = false;
        }

        private void SetupDriver(object sender, RoutedEventArgs e) {
            DxxDriverManager.Instance.Setup(ViewModel.Driver);
        }
    }
}
