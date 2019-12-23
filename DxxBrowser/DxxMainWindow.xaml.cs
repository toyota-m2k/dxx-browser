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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DxxBrowser {
    public class DxxMainViewModel : DxxViewModelBase, IDisposable {
        #region Properties
        public ReactiveProperty<bool> IsDownloading { get; } = DxxDownloader.Instance.Busy.ToReactiveProperty();

        public ReactiveProperty<ObservableCollection<DxxTargetInfo>> TargetList { get; } = new ReactiveProperty<ObservableCollection<DxxTargetInfo>>(new ObservableCollection<DxxTargetInfo>());
        public ReactiveProperty<ObservableCollection<DxxDownloadingItem>> DownloadingList { get; } = new ReactiveProperty<ObservableCollection<DxxDownloadingItem>>(DxxDownloader.Instance.DownloadingStateList);
        public ReactiveProperty<ObservableCollection<DxxLogInfo>> StatusList { get; } = new ReactiveProperty<ObservableCollection<DxxLogInfo>>(DxxLogger.Instance.LogList);
        public DxxBookmark Bookmarks { get; } = DxxBookmark.Deserialize();

        public ReactiveProperty<bool> ShowTargetList { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowDownloadingList { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> ShowStatusList { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> ShowSubBrowser { get; } = new ReactiveProperty<bool>(true);

        private void InitializeProperties() {
            TargetList.Subscribe((v) => {
                if (v.Count > 0) {
                    ShowTargetList.Value = true;
                }
            });
        }

        #endregion

        #region Commands
        public ReactiveCommand DownloadByTargetList { get; } = new ReactiveCommand();
        public ReactiveCommand CancellAllCommand { get; } = new ReactiveCommand();

        public ReactiveCommand ClearStatusCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClearDownloadingListCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClearTargetListCommand { get; } = new ReactiveCommand();

        private void InitializeCommands() {
            ClearStatusCommand.Subscribe(() => {
                StatusList.Value.Clear();
            });

            /**
             * ターゲットリスト内のアイテムをすべてDL開始
             */
            DownloadByTargetList.Subscribe(() => {
                DxxUrl.DownloadTargets(TargetList.Value);
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
        }

        #endregion

        #region Initialize/Terminate

        public DxxMainViewModel(DxxMainWindow owner) {
            mOwner = new WeakReference<DxxMainWindow>(owner);
            InitializeCommands();
            InitializeProperties();
        }

        public override void Dispose() {
            base.Dispose();
        }
        #endregion

        #region Other Props/Fields

        private WeakReference<DxxMainWindow> mOwner;
        private DxxMainWindow Owner => mOwner?.GetValue();
        private Window OwnerWindow => Window.GetWindow(Owner);

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

        public DxxMainWindow() {
            DxxDownloader.Instance.Initialize(this);
            DxxLogger.Instance.Initialize(this);
            DxxDriverManager.Instance.LoadSettings(Window.GetWindow(this));
            ViewModel = new DxxMainViewModel(this);
            InitializeComponent();
        }
        private void OnLoaded(object sender, RoutedEventArgs e) {
            mainViewer.Initialize(true, ViewModel.Bookmarks, ViewModel.TargetList);
            subViewer.Initialize(false, ViewModel.Bookmarks, ViewModel.TargetList);
            mainViewer.ViewModel.RequestLoadInSubview.Subscribe((v) => {
                Dispatcher.InvokeAsync(() => {
                    subViewer.ViewModel.NavigateCommand.Execute(v);
                });
            });
            subViewer.ViewModel.RequestLoadInMainView.Subscribe((v) => {
                Dispatcher.InvokeAsync(() => {
                    mainViewer.ViewModel.NavigateCommand.Execute(v);
                });
            });
            ViewModel.DownloadingList.Value.CollectionChanged += OnListChanged<DxxDownloadingItem>;
            ViewModel.StatusList.Value.CollectionChanged += OnListChanged<DxxLogInfo>;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
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

        private void OnClosing(object sender, CancelEventArgs e) {
            ViewModel.Bookmarks.Serialize();
            _ = TerminateAll();
            while (DxxDownloader.Instance.IsBusy||DxxActivityWatcher.Instance.IsBusy) {
                MessageBox.Show("なんかやってるので終了できません。", "DXX Browser", MessageBoxButton.OK);
            }
        }

        private async Task TerminateAll() {
            await DxxActivityWatcher.Instance.TerminateAsync(true);
            await DxxDownloader.Instance.TerminateAsync(true);
        }

        private void OnDownloadedItemActivate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var di = (sender as ListViewItem)?.Content as DxxDownloadingItem;
            if (di != null) {
                if (di.Status == DxxDownloadingItem.DownloadStatus.Completed) {
                    var driver = DxxDriverManager.Instance.FindDriver(di.Url);
                    if (driver != null) {
                        var file = driver.StorageManager?.GetSavedFile(new Uri(di.Url));
                        if (file != null) {
                            var proc = new Process();
                            proc.StartInfo.FileName = file;
                            proc.StartInfo.UseShellExecute = true;
                            proc.Start();
                        }
                    }
                } else if (di.Status != DxxDownloadingItem.DownloadStatus.Downloading) {
                    // retry
                    var driver = DxxDriverManager.Instance.FindDriver(di.Url);
                    if (driver != null) {
                        var dxxUrl = new DxxUrl(new Uri(di.Url), driver, di.Name, di.Description);
                        _ = dxxUrl.Download();
                    }
                }
            }
        }

        private async void OnTargetItemActivate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var ti = (sender as ListViewItem)?.Content as DxxTargetInfo;
            if (ti != null) {
                var driver = DxxDriverManager.Instance.FindDriver(ti.Url);
                if (driver != null) {
                    var dxxUrl = new DxxUrl(ti, driver);
                    await dxxUrl.Download();
                }
            }
        }
    }
}
