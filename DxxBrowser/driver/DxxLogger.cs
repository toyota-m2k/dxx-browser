using System;
using System.Collections.Generic;
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
        public 

        private DxxLogger() {

        }
    }
}
