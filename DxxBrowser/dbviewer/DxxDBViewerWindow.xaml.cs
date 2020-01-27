using Common;
using DxxBrowser.driver;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DxxBrowser {
    /// <summary>
    /// DxxDBViewerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxDBViewerWindow : Window
    {
        class DBViewModel : MicViewModelBase<DxxDBViewerWindow>, DxxPlayer.IPlayerOwner {
            #region Properties

            public ReactiveProperty<ObservableCollection<DxxDBStorage.DBRecord>> List { get; } = new ReactiveProperty<ObservableCollection<DxxDBStorage.DBRecord>>(new ObservableCollection<DxxDBStorage.DBRecord>());

            #endregion

            #region Commands

            public ReactiveCommand RefreshCommand { get; } = new ReactiveCommand();
            public ReactiveCommand RetryDownloadCommand { get; } = new ReactiveCommand();
            public ReactiveCommand PlayCommand { get; } = new ReactiveCommand();

            public ReactiveCommand DeleteAndBlockCommand { get; } = new ReactiveCommand();
            public ReactiveCommand ResetAndDownloadCommand { get; } = new ReactiveCommand();

            #endregion

            /**
             * 初期化
             */
            public DBViewModel(DxxDBViewerWindow owner) : base(owner) {
                RefreshCommand.Subscribe(RefreshDB);
                RetryDownloadCommand.Subscribe(RetryDownload);
                PlayCommand.Subscribe(ShowPlayer);
                DeleteAndBlockCommand.Subscribe(DeleteAndBlock);
                ResetAndDownloadCommand.Subscribe(ResetAndDownload);
                PlayList = new DBPlayList(this);
                DxxDBStorage.Instance.DBUpdated += OnDBUpdated;
            }

            private void ResetAndDownload() {
                Debug.WriteLine("reset and download");
                var list = Owner?.mListView?.SelectedItems.ToEnumerable<DxxDBStorage.DBRecord>()?
                                .Where(v => v.Status == DxxDBStorage.DLStatus.FATAL_ERROR || v.Status == DxxDBStorage.DLStatus.FORBIDDEN || v.Status == DxxDBStorage.DLStatus.RESERVED)?
                                .Select((v) => new DxxTargetInfo(v.Url, v.Name, v.Description))
                                .ToList();  // UnregisterNG と、Download で、２つのforeachが行われ、IEnumerableでは対応できない。

                if (!Utils.IsNullOrEmpty(list)) {
                    DxxNGList.Instance.UnregisterNG(list.Select(v => v.Url));
                    DxxDriverManager.Instance.Download(list);
                }
            }

            private void DeleteAndBlock() {
                Debug.WriteLine("delete and block");
                var list = Owner?.mListView?.SelectedItems.ToEnumerable<DxxDBStorage.DBRecord>()?
                                 .Where((v) => v.Status == DxxDBStorage.DLStatus.COMPLETED || v.Status == DxxDBStorage.DLStatus.RESERVED)
                                 .ToList(); // RegisterNG 中に選択状態が変わるので、ToList()でリストを確定しておく。
                if (!Utils.IsNullOrEmpty(list)) {
                    using (DxxDBStorage.Instance.Transaction()) {
                        foreach (var v in list) {
                            DxxNGList.Instance.RegisterNG(v.Url);
                        }
                    }
                }
            }

            public override void Dispose() {
                base.Dispose();
                DxxDBStorage.Instance.DBUpdated -= OnDBUpdated;
            }

            private void OnDBUpdated(DxxDBStorage.DBModification type, DxxDBStorage.DBRecord rec) {
                switch(type) {
                    case DxxDBStorage.DBModification.APPEND:
                        List.Value.Add(rec);
                        Sort(DxxGlobal.Instance.SortInfo, true);
                        break;
                    case DxxDBStorage.DBModification.UPDATE:
                        var r = List.Value.Where((v) => v.ID == rec.ID);
                        if(!Utils.IsNullOrEmpty(r)) {
                            r.First().CopyFrom(rec);
                        }
                        Sort(DxxGlobal.Instance.SortInfo, true);
                        break;
                    case DxxDBStorage.DBModification.REMOVE:
                        List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(List.Value.Where((v) => v.ID != rec.ID));
                        UpdatePlayList();
                        break;
                }
            }

            //public void LoadData() {
            //    RefreshDB();
            //}

            /**
             * ソートする
             */
            public void Sort(SortInfo next, bool force = false) {
                if (next == null) {
                    return;
                }
                var prev = DxxGlobal.Instance.SortInfo;
                if (!force && prev == next) {
                    return;
                }
                DxxGlobal.Instance.SortInfo = next;
                if (!force && prev.Key == next.Key) {
                    if (prev.Order != next.Order) {
                        List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(List.Value.Reverse());
                    }
                } else {
                    var cmp = new RecordComparer(next);
                    List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(List.Value.OrderBy((v) => v, cmp));
                }
                Owner.UpdateColumnHeaderOnSort(next);

                // maintainance of PlayList
                UpdatePlayList();
            }

            private void UpdatePlayList() {
                var selected = Owner.mListView.SelectedItem as DxxDBStorage.DBRecord;
                if (null == selected) {
                    PlayList.Current.Value = selected;
                } else {
                    ((DBPlayList)PlayList).UpdatePlayList(selected);
                }
            }

            public void LoadData() {
                RefreshDB();
            }

            /**
             * DBを読み直す
             */
            private void RefreshDB() {
                List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(DxxDBStorage.Instance.ListAll());
                Sort(DxxGlobal.Instance.SortInfo, true);
            }

            /**
             * 完了していない項目のダウンロードを再実行
             */
            private void RetryDownload() {
                var list = DxxDBStorage.Instance.ListForRetry().Select((v) => {
                    return new DxxTargetInfo(v.Url, v.Name, v.Description);
                });
                DxxDriverManager.Instance.Download(list);
            }

            private DxxPlayer Player;

            /**
             * プレーヤーを表示
             */
            private void ShowPlayer() {
                if (null == Player) {
                    Player = DxxPlayer.ShowPlayer(this);
                }
            }

            #region IPlayerOwner

            public void PlayerClosed(DxxPlayer player) {
                if (Player == player) {
                    Player = null;
                }
            }

            public Window OwnerWindow => Window.GetWindow(Owner);

            //[Disposal(false)]
            //public IDxxPlayList PlayList => this;

            public IDxxPlayList PlayList { get; }

            #endregion

            #region PlayList

            class DBPlayList : MicViewModelBase<DBViewModel>, IDxxPlayList {
                public DBPlayList(DBViewModel model) : base(owner:model, disposeNonPublic:true) {
                    Current.Subscribe(UpdatePlayList);
                    ItemRemoving = DxxNGList.Instance.AsPlayItemRemovingObservable().Subscribe(OnRemovingItem);
                }

                // ListView の SelectedItemn にバインドする
                public ReactiveProperty<IDxxPlayItem> Current { get; } = new ReactiveProperty<IDxxPlayItem>();

                public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
                public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
                public ReactiveProperty<int> CurrentPos { get; } = new ReactiveProperty<int>(0);
                public ReactiveProperty<int> TotalCount { get; } = new ReactiveProperty<int>(0);

                //public void AddSource(IDxxPlayItem source) {
                //}

                //public void DeleteSource(IDxxPlayItem source) {
                //    if(source.Url==Current.Value.Url) {
                //        if (!Next() && !Prev()) {
                //            Current.Value = null;
                //        }
                //    }
                //    DxxNGList.Instance.RegisterNG(source.Url);
                //    DxxDBStorage.Instance.DLPlayList.DeleteSource(source);
                //}

                // （使わないけど）MicViewModelBase で Disposeされるようにプロパティとして保持しておく。
                private IDisposable ItemRemoving { get; }

                /**
                 * DBアイテム（ファイル）が削除される前に呼び出される。
                 * --> 現在再生中なら、インデックスを進めるか、戻すかして、カレントアイテムが削除されるのを回避する。
                 */
                private void OnRemovingItem(string url) {
                    if (url == Current.Value.Url) {
                        if (!Next() && !Prev()) {
                            Current.Value = null;
                        }
                    }
                }

                //private int CurrentIndex {
                //    get {
                //        if (Owner.List.Value.Count > 0) {
                //            var item = Current.Value;
                //            if (null != item) {
                //                int index = Owner.List.Value.IndexOf((DxxDBStorage.DBRecord)item);
                //                if (index >= 0) {
                //                    return index;
                //                }
                //            }
                //            Current.Value = Owner.List.Value[0];
                //        }
                //        return 0;
                //    }
                //}

                public bool Next() {
                    var index = Owner.List.Value.IndexOf((DxxDBStorage.DBRecord)Current.Value);
                    if(index<0) {
                        index = 0;
                    } else {
                        index++;
                    }

                    if (index< Owner.List.Value.Count) {
                        Current.Value = Owner.List.Value[index];
                        return true;
                    }
                    return false;
                }

                public bool Prev() {
                    var index = Owner.List.Value.IndexOf((DxxDBStorage.DBRecord)Current.Value);
                    index--;
 
                    if (0<= index && index< Owner.List.Value.Count) {
                        Current.Value = Owner.List.Value[index];
                        return true;
                    }
                    return false;
                }

                private void UpdateDependentProperties(int currentIndex) {
                    int totalCount = Owner.List.Value.Count;
                    CurrentPos.Value = currentIndex + 1;
                    TotalCount.Value = totalCount;
                    HasNext.Value = 0 < totalCount && currentIndex + 1 < totalCount;
                    HasPrev.Value = 0 < totalCount && 0 < currentIndex;
                }

                public void UpdatePlayList(IDxxPlayItem selectedItem) {
                    var index = Owner.List.Value.IndexOf((DxxDBStorage.DBRecord)selectedItem);
                    UpdateDependentProperties(index);
                }
            }

            #endregion
        }

        DBViewModel ViewModel {
            get => DataContext as DBViewModel;
            set => DataContext = value;
        }

        public DxxDBViewerWindow() {
            ViewModel = new DBViewModel(this);
            InitializeComponent();
        }

        public static void ShowWindow(Window owner) {
            var win = new DxxDBViewerWindow();
            win.Owner = owner;
            win.Show();
        }

        #region Sort

        public enum SortKey {
            ID,
            Status,
            URL,
            Path,
            Name,
            Desc,
            Driver,
            Flags,
            Date,
        }

        public enum SortOrder {
            ASCENDING,
            DESCENDING,
        }

        public class SortInfo {
            public SortKey Key { get; set; } = SortKey.ID;
            public SortOrder Order { get; set; } = SortOrder.ASCENDING;
            public SortInfo() {
            }
            public SortInfo(SortKey key, SortOrder order) {
                Key = key;
                Order = order;
            }
        }

        private SortKey HeaderName2SortKey(string name) {
            switch (name) {
                case "Name": return SortKey.Name;
                case "Path": return SortKey.Path;
                case "URL": return SortKey.URL;
                case "Desc": return SortKey.Desc;
                case "Status": return SortKey.Status;
                case "Flags": return SortKey.Flags;
                case "Driver": return SortKey.Driver;
                case "Date": return SortKey.Date;
                case "ID":
                default: return SortKey.ID;
            }
        }

        //private string SortKey2HeaderName(SortKey key) {
        //    return $"{key}";
        //}

        private void OnHeaderClick(object sender, RoutedEventArgs e) {
            var header = e.OriginalSource as GridViewColumnHeader;
            if (null == header) {
                return;
            }
            SortKey key = HeaderName2SortKey(header.Content.ToString());

            var prev = DxxGlobal.Instance.SortInfo;
            var next = new SortInfo();
            next.Key = key;
            if (prev.Key == key) {
                next.Order = prev.Order == SortOrder.ASCENDING ? SortOrder.DESCENDING : SortOrder.ASCENDING;
            }
            ViewModel.Sort(next);
        }

        private class RecordComparer : IComparer<DxxDBStorage.DBRecord> {
            private SortInfo SI;
            public RecordComparer(SortInfo si) {
                SI = si;
            }
            public int Compare(string x, string y) {
                return string.Compare(x, y);
            }
            public int Compare(long x, long y) {
                var d = x - y;
                return (d < 0) ? -1 : (d > 0) ? 1 : 0;
            }
            public int Compare(int x, int y) {
                return x - y;
            }
            public int Compare(DxxDBStorage.DBRecord x, DxxDBStorage.DBRecord y) {
                int r = 0;
                switch (SI.Key) {
                    case SortKey.Name:
                        r = Compare(x.Name, y.Name);
                        break;
                    case SortKey.Path:
                        r = Compare(x.Path, y.Path);
                        break;
                    case SortKey.URL:
                        r = Compare(x.Url, y.Url);
                        break;
                    case SortKey.Desc:
                        r = Compare(x.Description, y.Description);
                        break;
                    case SortKey.ID:
                        r = Compare(x.ID, y.ID);
                        break;
                    case SortKey.Status:
                        r = Compare((int)x.Status, (int)y.Status);
                        break;
                    case SortKey.Flags:
                        r = Compare(x.Flags, y.Flags);
                        break;
                    case SortKey.Driver:
                        r = Compare(x.Driver, y.Driver);
                        break;
                    case SortKey.Date:
                        r = Compare(x.Date.ToFileTimeUtc(), y.Date.ToFileTimeUtc());
                        break;
                }
                return (SI.Order == SortOrder.ASCENDING) ? r : -r;
            }
        }

        private Dictionary<SortKey, GridViewColumnHeader> mHeaderColumnDic = null;

        private bool InitHeaderColumnDic() {
            if(null == mListView) {
                return false;
            }
            if (null == mHeaderColumnDic) {
                mHeaderColumnDic = new Dictionary<SortKey, GridViewColumnHeader>(10);
                foreach (var header in Utils.FindVisualChildren<GridViewColumnHeader>(mListView)) {
                    Debug.WriteLine(header.ToString());
                    var textBox = Utils.FindVisualChildren<TextBlock>(header).FirstOrDefault();
                    if (null != textBox) {
                        var key = HeaderName2SortKey(textBox.Text);
                        mHeaderColumnDic[key] = header;
                    }
                }
            }
            return true;
        }

        private void UpdateColumnHeaderOnSort(SortInfo info) {
            if(!InitHeaderColumnDic()) {
                return;
            }

            foreach (var v in mHeaderColumnDic) {
                if (v.Key == info.Key) {
                    v.Value.Tag = info.Order == SortOrder.ASCENDING ? "asc" : "desc";
                } else {
                    v.Value.Tag = null;
                }
            }
        }

        #endregion



        private void OnItemSelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        private void OnListItemDoubleClick(object sender, MouseButtonEventArgs e) {

        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            //UpdateColumnHeaderOnSort(DxxGlobal.Instance.SortInfo);
            ViewModel.LoadData();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel?.Dispose();
            ViewModel = null;
        }
    }
}
