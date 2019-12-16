using DxxBrowser.driver;
using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DxxBrowser {
    public enum DxxClickMode {
        NaviMode,
        DLMode,
    }

    public class DxxMainViewModel : DxxViewModelBase, IDisposable {
        #region Properties
        public ReactiveProperty<DxxClickMode> ClickMode { get; } = new ReactiveProperty<DxxClickMode>(DxxClickMode.DLMode);
        public ReactiveProperty<bool> NaviToSubView { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> DLAndSubView { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<string> MainUrl { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> SubUrl { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<bool> Loaded { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);

        public delegate void NavigateToProc(string url);
        public NavigateToProc NavigateTo;

        public ReactiveProperty<string> DriverName { get; } = new ReactiveProperty<string>(DxxDriverManager.DEFAULT.Name);
        public ReactiveProperty<bool> SettingEnabled { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> IsTarget { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainerList { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> SubViewDownloadable { get; } = new ReactiveProperty<bool>(false);


        public ReactiveProperty<bool> IsDownloading { get; } = DxxDownloader.Instance.Busy.ToReactiveProperty();

        public ReactiveProperty<ObservableCollection<DxxTargetInfo>> TargetList { get; } = new ReactiveProperty<ObservableCollection<DxxTargetInfo>>(new ObservableCollection<DxxTargetInfo>());
        public ReactiveProperty<ObservableCollection<DxxDownloadingItem>> DownloadingList { get; } = new ReactiveProperty<ObservableCollection<DxxDownloadingItem>>(DxxDownloader.Instance.DownloadingStateList);
        public ReactiveProperty<ObservableCollection<DxxLogInfo>> StatusList { get; } = new ReactiveProperty<ObservableCollection<DxxLogInfo>>(DxxLogger.Instance.LogList);

        public ReactiveProperty<bool> ShowTargetList { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowDownloadingList { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> ShowStatusList { get; } = new ReactiveProperty<bool>(true);

        public ReactiveProperty<bool> ShowSubBrowser { get; } = new ReactiveProperty<bool>(true);

        public ReactiveProperty<DxxBookmark> Bookmarks { get; } = new ReactiveProperty<DxxBookmark>(DxxBookmark.Deserialize());
        public ReactiveProperty<bool> IsBookmarked { get; } = new ReactiveProperty<bool>(false);

        private void InitializeProperties() {
            MainUrl.Subscribe((v) => {
                var driver = DxxDriverManager.Instance.FindDriver(v);
                if (driver != null) {
                    var uri = new Uri(v);
                    mDxxMainUrl = new DxxUrl(uri, driver, Driver.GetNameFromUri(uri, "main"), "");
                    Driver = driver;
                } else {
                    mDxxMainUrl = null;
                    Driver = DxxDriverManager.DEFAULT;
                }
                UpdateContent();
                IsBookmarked.Value = Bookmarks.Value.FindBookmark(v) != null;
            });
            SubUrl.Subscribe((v) => {
                if (Driver.IsSupported(v)) {
                    var uri = new Uri(v);
                    mDxxSubUrl = new DxxUrl(uri, Driver, Driver.GetNameFromUri(uri, "sub"), "");
                    SubViewDownloadable.Value = mDxxSubUrl.IsContainer || mDxxSubUrl.IsTarget;
                    ShowSubBrowser.Value = true;
                } else {
                    mDxxSubUrl = null;
                    SubViewDownloadable.Value = false;
                }
            });
            IsDownloading.Subscribe((v) => {
                Debug.WriteLine($"IsDownloading={v}");
            });

        }

        #endregion

        #region Commands

        public ReactiveCommand<string> NavigateCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand SetupDriverCommand { get; } = new ReactiveCommand();

        public ReactiveCommand ExtractTargetListCommand { get; } = new ReactiveCommand();

        public ReactiveCommand DownloadCommand { get; } = new ReactiveCommand();
        public ReactiveCommand DownloadSubCommand { get; } = new ReactiveCommand();
        public ReactiveCommand DownloadByTargetList { get; } = new ReactiveCommand();
        public ReactiveCommand CancellAllCommand { get; } = new ReactiveCommand();

        public ReactiveCommand ClearStatusCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClearDownloadingListCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClearTargetListCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClearURLCommand { get; } = new ReactiveCommand();

        public ReactiveCommand BookmarkChanged { get; } = new ReactiveCommand();

        private void InitializeCommands() {
            NavigateCommand.Subscribe((v) => {
                NavigateTo?.Invoke(v);
            });
            ClearStatusCommand.Subscribe(() => {
                StatusList.Value.Clear();
            });
            SetupDriverCommand.Subscribe(() => {
                DxxDriverManager.Instance.Setup(Driver);
            });
            // カレントURLからターゲットをダウンロード
            DownloadCommand.Subscribe(() => {
                _ = DxxMainUrl.Download();
            });
            DownloadSubCommand.Subscribe(() => {
                _ = DxxSubUrl?.Download();
            });

            /**
             * ターゲットリスト内のアイテムをすべてDL開始
             */
            DownloadByTargetList.Subscribe(() => {
                DxxUrl.DownloadTargets(Driver, TargetList.Value);
            });

            // TargetListを抽出してリストに出力
            ExtractTargetListCommand.Subscribe(async () => {
                var targets = await DxxMainUrl.TryGetTargetContainers();
                if (targets!=null && targets.Count > 0) {
                    TargetList.Value = new ObservableCollection<DxxTargetInfo>(targets);
                    ShowTargetList.Value = true;
                } else {
                    TargetList.Value?.Clear();
                }
            });
            CancellAllCommand.Subscribe(() => {
                DxxActivityWatcher.Instance.CancelAll();
                _ = DxxDownloader.Instance.CancelAllAsync();
            });
            ClearDownloadingListCommand.Subscribe(() => {
                DownloadingList.Value.Clear();
            });
            ClearTargetListCommand.Subscribe(() => {
                TargetList.Value.Clear();
            });
            ClearURLCommand.Subscribe(() => {
                MainUrl.Value = "";
                View.urlInput.Focus();
            });

            BookmarkChanged.Subscribe(() => {
                var url = MainUrl.Value;
                if (string.IsNullOrEmpty(url)) {
                    IsBookmarked.Value = false;
                    return;
                }
                if(IsBookmarked.Value) {
                    Bookmarks.Value.AddBookmark("", url);
                } else {
                    Bookmarks.Value.RemoveBookmark(url);
                    MainUrl.Value = url;
                }
            });
        }

        #endregion

        #region Initialize/Terminate

        public DxxMainViewModel(DxxMainWindow owner) {
            mView = new WeakReference<DxxMainWindow>(owner);
            InitializeCommands();
            InitializeProperties();
        }
        private void UpdateContent() {
            if (mDxxMainUrl == null) {
                IsTarget.Value = false;
                IsContainer.Value = false;
                IsContainerList.Value = false;
            } else {
                IsTarget.Value = Driver.LinkExtractor.IsTarget(mDxxMainUrl);
                IsContainer.Value = Driver.LinkExtractor.IsContainer(mDxxMainUrl);
                IsContainerList.Value = Driver.LinkExtractor.IsContainerList(mDxxMainUrl);
            }
        }

        public override void Dispose() {
            base.Dispose();
            NavigateTo = null;
        }
        #endregion

        #region Other Props/Fields

        private DxxUrl mDxxSubUrl = null;
        private DxxUrl mDxxMainUrl = null;
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

        public DxxUrl DxxMainUrl => mDxxMainUrl;
        public DxxUrl DxxSubUrl => mDxxSubUrl;

        private WeakReference<DxxMainWindow> mView;
        private DxxMainWindow View => mView?.GetValue();

        #endregion
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
            DxxDownloader.Instance.Initialize(this);
            DxxLogger.Instance.Initialize(this);
            ViewModel = new DxxMainViewModel(this);
            ViewModel.NavigateTo = NavigateTo;
            InitializeComponent();
        }
        private void OnLoaded(object sender, RoutedEventArgs e) {
            mainBrowser.NavigationStarting += WebView_NavigationStarting;
            mainBrowser.NavigationCompleted += WebView_NavigationCompleted;

            ViewModel.DownloadingList.Value.CollectionChanged += OnListChanged<DxxDownloadingItem>;
            ViewModel.StatusList.Value.CollectionChanged += OnListChanged<DxxLogInfo>;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
            mainBrowser.NavigationStarting -= WebView_NavigationStarting;
            mainBrowser.NavigationCompleted -= WebView_NavigationCompleted;
            ViewModel.DownloadingList.Value.CollectionChanged -= OnListChanged<DxxDownloadingItem>;
            ViewModel.StatusList.Value.CollectionChanged -= OnListChanged<DxxLogInfo>;
        }

        private void OnListChanged<T>(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add) {
                var list = sender as ObservableCollection<T>;
                if (list.Count > 0) {
                    var last = list.Last();
                    if (last is DxxLogInfo) {
                        statusList.ScrollIntoView(last);
                    } else if(last is DxxDownloadingItem) {
                        downloadingList.ScrollIntoView(last);
                    }
                }
            }
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



        private void WebView_NavigationStarting(object sender, WebViewControlNavigationStartingEventArgs e) {
            if (!mLoadingMain) {
                if (ViewModel.IsContainerList.Value) {
                    switch (ViewModel.ClickMode.Value) {
                        case DxxClickMode.NaviMode:
                            if(ViewModel.NaviToSubView.Value) {
                                subBrowser.Stop();
                                subBrowser.Navigate(e.Uri);
                                ViewModel.SubUrl.Value = e.Uri.ToString();
                                e.Cancel = true;
                                return;
                            }
                            break;
                        case DxxClickMode.DLMode:
                            var driver = DxxDriverManager.Instance.FindDriver(e.Uri.ToString());
                            if (driver != null) {
                                var du = new DxxUrl(e.Uri, driver, driver.GetNameFromUri(e.Uri, "link"), "");
                                if (du.IsContainer || du.IsTarget) {
                                    if (ViewModel.DLAndSubView.Value) {
                                        subBrowser.Stop();
                                        subBrowser.Navigate(e.Uri);
                                        ViewModel.SubUrl.Value = e.Uri.ToString();
                                    }
                                    _ = du.Download();
                                    e.Cancel = true;
                                    return;
                                }
                            }
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

        private bool WillShutdown = false;
        private void OnClosing(object sender, CancelEventArgs e) {
            if(WillShutdown) {
                return;
            }

            e.Cancel = true;
            if (DxxDownloader.Instance.IsBusy||DxxActivityWatcher.Instance.IsBusy) {
                var r = MessageBox.Show("終了しますか？", "DXX Browser", MessageBoxButton.YesNo);
                if(r== MessageBoxResult.Cancel) {
                    return;
                }
            }
            ViewModel.Bookmarks.Value.Serialize();
            CloseAnyway();
        }
        private async void CloseAnyway() {
            WillShutdown = true;
            await DxxActivityWatcher.Instance.TerminateAsync(true);
            await DxxDownloader.Instance.TerminateAsync(true);
            //System.Windows.Application.Current.Shutdown();
            Environment.Exit(0);
        }

        private void OnDownloadedItemActivate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var di = (sender as ListViewItem)?.Content as DxxDownloadingItem;
            if(di!=null && di.Status==DxxDownloadingItem.DownloadStatus.Completed) {
                var file = ViewModel.Driver?.StorageManager?.GetSavedFile(new Uri(di.Url));
                if (file != null) {
                    var proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = file;
                    proc.StartInfo.UseShellExecute = true;
                    proc.Start();
                }
            }
        }

        private async void OnTargetItemActivate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var ti = (sender as ListViewItem)?.Content as DxxTargetInfo;
            if (ti != null) {
                var dxxUrl = new DxxUrl(ti, ViewModel.Driver);
                await dxxUrl.Download();
            }
        }
    }
}
