using Common;
using DxxBrowser.driver;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using System.Windows.Shapes;

namespace DxxBrowser
{
    /// <summary>
    /// DxxDBViewerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxDBViewerWindow : Window
    {
        class DBViewModel : MicViewModelBase {
            public ReactiveProperty<ObservableCollection<DxxDBStorage.DBRecord>> List { get; } = new ReactiveProperty<ObservableCollection<DxxDBStorage.DBRecord>>();

            public DBViewModel() {
                List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(DxxDBStorage.Instance.ListAll());
            }
        }

        DBViewModel ViewModel {
            get => DataContext as DBViewModel;
            set => DataContext = value;
        }

        public DxxDBViewerWindow() {
            ViewModel = new DBViewModel();
            InitializeComponent();
        }

        public static void ShowWindow(Window owner) {
            var win = new DxxDBViewerWindow();
            win.Owner = owner;
            win.Show();
        }

        public enum SortKey {
            ID,
            Status,
            URL,
            Path,
            Name,
            Desc,
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
                case "ID":
                default: return SortKey.ID;
            }
        }

        private string SortKey2HeaderName(SortKey key) {
            return $"{key}";
        }

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
            ExecSort(next);
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
                var d = y - x;
                return (d < 0) ? -1 : (d > 0) ? 1 : 0;
            }
            public int Compare(int x, int y) {
                return y - x;
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
                        r = Compare(x.Desc, y.Desc);
                        break;
                    case SortKey.ID:
                        r = Compare(x.ID, y.ID);
                        break;
                    case SortKey.Status:
                        r = Compare((int)x.Status, (int)y.Status);
                        break;
                }
                return (SI.Order == SortOrder.ASCENDING) ? r : -r;
            }
        }

        private Dictionary<SortKey, GridViewColumnHeader> mHeaderColumnDic = null;

        private void InitHeaderColumnDic() {
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
        }

        private void UpdateColumnHeaderOnSort(SortInfo info) {
            InitHeaderColumnDic();
            foreach (var v in mHeaderColumnDic) {
                if (v.Key == info.Key) {
                    v.Value.Tag = info.Order == SortOrder.ASCENDING ? "asc" : "desc";
                } else {
                    v.Value.Tag = null;
                }
            }
        }

        private void ExecSort(SortInfo next, bool force = false) {
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
                    ViewModel.List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(ViewModel.List.Value.Reverse());
                }
            } else {
                var cmp = new RecordComparer(next);
                ViewModel.List.Value = new ObservableCollection<DxxDBStorage.DBRecord>(ViewModel.List.Value.OrderBy((v) => v, cmp));
            }
            UpdateColumnHeaderOnSort(next);
        }

        private void OnFileItemSelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        private void OnListItemDoubleClick(object sender, MouseButtonEventArgs e) {

        }
    }
}
