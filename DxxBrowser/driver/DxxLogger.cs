using Common;
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace DxxBrowser.driver {
    /**
     * View Model
     */
    public class DxxLogInfo : MicViewModelBase {
        #region Constants

        /**
         * ログの種別
         */
        public enum LogType {
            COMMENT,
            SUCCESS,
            CANCEL,
            ERROR,
        }

        private static Brush CommentColor = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        private static Brush ErrorColor = new SolidColorBrush(Color.FromRgb(216, 0, 0));
        private static Brush CancelColor = new SolidColorBrush(Color.FromRgb(255, 128, 0));
        private static Brush SuccessColor = new SolidColorBrush(Color.FromRgb(0, 192, 0));

        #endregion

        #region Properties

        public DateTime Time { get; }
        public LogType Type { get; }
        public string Category { get; }
        public string Message { get; }

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
        #endregion

        public DxxLogInfo(LogType type, string category, string msg) {
            Time = DateTime.Now;
            Type = type;
            Message = msg;
            Category = category;
        }
    }

    public class DxxLogger {
        #region Properties

        public ObservableCollection<DxxLogInfo> LogList { get; } = new ObservableCollection<DxxLogInfo>();

        #endregion

        #region Private Fields

        private WeakReference<DispatcherObject> mDispatherSource;
        private Dispatcher Dispatcher => mDispatherSource?.GetValue()?.Dispatcher;

        #endregion

        #region Singleton

        public static DxxLogger Instance { get; private set; }
        public static void Initialize(DispatcherObject dispatcherSource) {
            Instance = new DxxLogger(dispatcherSource);
        }

        private DxxLogger(DispatcherObject dispatcherSource) {
            mDispatherSource = new WeakReference<DispatcherObject>(dispatcherSource);
        }

        #endregion

        #region Public Methods

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
        #endregion
    }
}
