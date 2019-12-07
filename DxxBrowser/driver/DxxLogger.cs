using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace DxxBrowser.driver {
    public class DxxLogInfo : DxxViewModelBase {
        public enum LogType {
            COMMENT,
            SUCCESS,
            CANCEL,
            ERROR,
        }

        public DateTime Time { get; }
        public LogType Type { get; }
        public string Category { get; }
        public string Message { get; }

        private static Brush CommentColor = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        private static Brush ErrorColor = new SolidColorBrush(Color.FromRgb(216, 0, 0));
        private static Brush CancelColor = new SolidColorBrush(Color.FromRgb(255,128,0));
        private static Brush SuccessColor = new SolidColorBrush(Color.FromRgb(0,192,0));

        public Brush TextColor {
            get {
                switch(Type) {
                    default:
                    case LogType.COMMENT:
                        return CommentColor;
                    case LogType.ERROR:
                        return ErrorColor;
                    case LogType.CANCEL:
                        return CancelColor;
                    case LogType.SUCCESS:
                        return SuccessColor;
                }
            }
        }

        public DxxLogInfo(LogType type, string category, string msg) {
            Time = DateTime.Now;
            Type = type;
            Message = msg;
            Category = category;
        }
    }

    public class DxxLogger {
        public ObservableCollection<DxxLogInfo> LogList { get; } = new ObservableCollection<DxxLogInfo>();
        private WeakReference<DispatcherObject> mDispatherSource;
        private Dispatcher Dispatcher => mDispatherSource?.GetValue()?.Dispatcher;

        public void Initialize(DispatcherObject dispatcherSource) {
            mDispatherSource = new WeakReference<DispatcherObject>(dispatcherSource);
        }

        public static DxxLogger Instance { get; } = new DxxLogger();

        private DxxLogger() {

        }

        public void Error(string category, string msg) {
            Dispatcher.Invoke(() => {
                LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.ERROR, category, msg));
            });
        }
        public void Comment(string category, string msg) {
            Dispatcher.Invoke(() => {
                LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.COMMENT, category, msg));
            });
        }
        public void Cancel(string category, string msg) {
            Dispatcher.Invoke(() => {
                LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.CANCEL, category, msg));
            });
        }
        public void Success(string category, string msg) {
            Dispatcher.Invoke(() => {
                LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.SUCCESS, category, msg));
            });
        }

    }
}
