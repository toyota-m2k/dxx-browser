using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxActivityWatcher {
        private int BusyCount = 0;
        private bool Closing = false;
        private TaskCompletionSource<object> ClosingTask = null;

        public static DxxActivityWatcher Instance { get; } = new DxxActivityWatcher();
        private DxxActivityWatcher() {

        }

        public bool IsBusy {
            get {
                lock(this) {
                    return BusyCount > 0;
                }
            }
        }

        void Add() {
            lock(this) {
                BusyCount++;
            }
        }

        void Release() {
            lock(this) {
                BusyCount--;
                if(0==BusyCount) {
                    if(ClosingTask != null) {
                        ClosingTask.TrySetResult(null);
                    }
                }
            }
        }

        public delegate Task<T> WatchingProc<T>();

        public async Task<T> Execute<T>(WatchingProc<T> proc, T defValue=null) where T :class {
            lock(this) {
                if(Closing) {
                    return defValue;
                }
                BusyCount++;
            }
            try {
                return await proc();
            } finally {
                Release();
            }
        }

        public Task TerminateAsync() {
            lock(this) {
                Closing = true;
                if(BusyCount==0) {
                    return Task.CompletedTask;
                }
                if(ClosingTask==null) {
                    ClosingTask = new TaskCompletionSource<object>();
                }
                return ClosingTask.Task;
            }
        }



    }
}
