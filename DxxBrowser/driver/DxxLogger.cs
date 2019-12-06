using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxLogInfo : DxxViewModelBase {
        public enum LogType {
            INFO,
            WARN,
            ERROR,
        }

        public DateTime Time { get; }
        public LogType Type { get; }
        public string Message { get; }

        public DxxLogInfo(LogType type, string msg) {
            Time = DateTime.Now;
            Type = type;
            Message = msg;
        }
    }

    public class DxxLogger {
        public ObservableCollection<DxxLogInfo> LogList { get; } = new ObservableCollection<DxxLogInfo>();

        public static DxxLogger Instance { get; } = new DxxLogger();

        private DxxLogger() {

        }

        public void Error(string msg) {
            LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.ERROR, msg));
        }
        public void Info(string msg) {
            LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.INFO, msg));
        }
        public void Warn(string msg) {
            LogList.Add(new DxxLogInfo(DxxLogInfo.LogType.WARN, msg));
        }
    }
}
