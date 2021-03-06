﻿using Common;
using DxxBrowser.driver;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DxxBrowser {
    /**
     * MainWindow のビューモデル
     */
    public class DxxMainViewModel : MicViewModelBase, IDisposable, DxxPlayer.IPlayerOwner {
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
        public ReactiveCommand ShowPlayerCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ShowDBViewerCommand { get; } = new ReactiveCommand();

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
                DxxDriverManager.Instance.Download(TargetList.Value);
            });

            CancellAllCommand.Subscribe(() => {
                DxxActivityWatcher.Instance.CancelAll();
                DxxDownloader.Instance.CancelAll();
            });
            ClearDownloadingListCommand.Subscribe(() => {
                DownloadingList.Value.Clear();
            });
            ClearTargetListCommand.Subscribe(() => {
                TargetList.Value.Clear();
            });
            ShowPlayerCommand.Subscribe(() => {
                if (Player == null) {
                    Player = DxxPlayer.ShowPlayer(this);
                }
            });
            ShowDBViewerCommand.Subscribe(() => {
                DxxDBViewerWindow.ShowWindow(OwnerWindow);
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
        public Window OwnerWindow => Window.GetWindow(Owner);

        #endregion

        #region Player

        private Window Player = null;
        public void PlayerClosed(DxxPlayer player) {
            if (Player == player) {
                Player = null;
            }
        }
        [Disposal(false)]
        public IDxxPlayList PlayList => DxxDBStorage.Instance?.DLPlayList;


        #endregion
    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxMainWindow : Window {
        /**
         * ビューモデル（DataContextにマップする）
         */
        public DxxMainViewModel ViewModel {
            get => DataContext as DxxMainViewModel;
            private set { DataContext = value; }
        }

        /**
         * コンストラクタ
         */
        public DxxMainWindow() {
            DxxActivityWatcher.Initialize();
            DxxDownloader.Initialize(this);
            DxxLogger.Initialize(this);
            DxxDriverManager.Initialize(this);

            //using (DxxDBStorage.Instance.Transaction()) {
            //    var list = DxxDBStorage.Instance.ListAll();
            //    foreach (var rec in list) {
            //        //DxxDBStorage.Instance.ComplementRecord(rec.ID, DxxDriverManager.Instance.FindDriver(rec.Url)?.Name ?? "UAV", 0);
            //        DxxDBStorage.Instance.UpdateTimestamp(rec.ID, rec.Path);
            //    }
            //}
            ViewModel = new DxxMainViewModel(this);

            InitializeComponent();
        }
        /**
         * ビュー作成後の初期化
         */
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
            ViewModel.DownloadingList.Value.CollectionChanged += OnDownloadListChanged;
            ViewModel.StatusList.Value.CollectionChanged += OnStatusListChanged;

            var version = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //Debug.WriteLine(v.ToString());
#if DEBUG
            string dbg = " <DBG> ";
#else
            string dbg = " ";
#endif
            this.Title = String.Format("{0}{5}(v{1}.{2}.{3}.{4})", version.ProductName, version.FileMajorPart, version.FileMinorPart, version.FileBuildPart, version.ProductPrivatePart,dbg);
        }

        /**
         * 後始末
         */
        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
            ViewModel.DownloadingList.Value.CollectionChanged -= OnDownloadListChanged;
            ViewModel.StatusList.Value.CollectionChanged -= OnStatusListChanged;
        }

        private static void ListScrollIntoView(ListView listView) {
            int lastIndex = listView.Items.Count - 1;
            if (lastIndex >= 0) {
                listView.ScrollIntoView(listView.Items[lastIndex]);
            }
        }

        private void OnDownloadListChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add) {
                ListScrollIntoView(downloadingList);
            }
        }

        private void OnStatusListChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add) {
                ListScrollIntoView(statusList);
            }
        }

        /**
         * ダウンロードリスト、ステータスリストが更新（アイテム追加）されたときにリスト最下部までスクロールする。
         */
        //private void OnListChanged<T>(object sender, NotifyCollectionChangedEventArgs e) {
        //    if (e.Action == NotifyCollectionChangedAction.Add) {
        //        var list = sender as ObservableCollection<T>;
        //        if (list.Count > 0) {
        //            var last = list.Last();
        //            if (last is DxxLogInfo) {
        //                statusList.ScrollIntoView(last);
        //            } else if (last is DxxDownloadingItem) {
        //                downloadingList.ScrollIntoView(last);
        //            }
        //        }
        //    }
        //}

        /**
         * アプリ終了時の処理
         */
        private void OnClosing(object sender, CancelEventArgs e) {
            ViewModel.Bookmarks.Serialize();
            _ = TerminateAll();
            while (DxxDownloader.IsBusy||DxxActivityWatcher.IsBusy) {
                MessageBox.Show("なんかやってるので終了できません。", "DXX Browser", MessageBoxButton.OK);
            }
        }

        /**
         * いろいろなものを終了・解放する
         */
        private async Task TerminateAll() {
            await DxxActivityWatcher.TerminateAsync(true);
            await DxxDownloader.TerminateAsync();
            DxxDriverManager.Terminate();
            DxxAnalysisWindow.Terminate();
        }

        /**
         * ダウンロードリストのアイテムのダブルクリックイベント
         * - ダウンロード済みなら関連付け起動
         * - エラーならダウンロードをリトライ
         */
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
                    DxxDriverManager.Instance.Download(di.Url, di.Name, di.Description);
                }
            }
        }

        /**
         * ターゲットリストのアイテムをダブルクリック
         * --> ダウンロード開始
         */
        private void OnTargetItemActivate(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var ti = (sender as ListViewItem)?.Content as DxxTargetInfo;
            if (ti != null) {
                DxxDriverManager.Instance.Download(ti.Url, ti.Name, ti.Description);
            }
        }
    }
}
