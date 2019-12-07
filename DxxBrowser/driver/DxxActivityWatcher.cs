using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public class DxxActivityWatcher {
        private bool Closing = false;
        private TaskCompletionSource<object> ClosingTask = null;
        private HashSet<CancellationTokenSource> CancellationTokenSources = new HashSet<CancellationTokenSource>();

        public static DxxActivityWatcher Instance { get; } = new DxxActivityWatcher();
        private DxxActivityWatcher() {

        }

        public bool IsBusy {
            get {
                lock(this) {
                    return CancellationTokenSources.Count > 0;
                }
            }
        }

        void Add(CancellationTokenSource cancellationTokenSource) {
            lock(this) {
                CancellationTokenSources.Add(cancellationTokenSource);
            }
        }

        void Release(CancellationTokenSource cancellationTokenSource) {
            lock(this) {
                CancellationTokenSources.Remove(cancellationTokenSource);
                if(0== CancellationTokenSources.Count) {
                    if(ClosingTask != null) {
                        ClosingTask.TrySetResult(null);
                    }
                }
            }
        }

        public delegate Task<T> WatchingProc<T>(CancellationToken cancellationToken);

        public async Task<T> Execute<T>(WatchingProc<T> proc, T defValue=null) where T :class {
            CancellationTokenSource cts;
            lock (this) {
                if(Closing) {
                    return defValue;
                }
                cts = new CancellationTokenSource();
                CancellationTokenSources.Add(cts);
            }
            try {
                return await proc(cts.Token);
            } finally {
                Release(cts);
            }
        }

        public Task TerminateAsync(bool cancelAll) {
            if(cancelAll) {
                CancelAll();
            }
            lock(this) {
                Closing = true;
                if(CancellationTokenSources.Count==0) {
                    return Task.CompletedTask;
                }
                if(ClosingTask==null) {
                    ClosingTask = new TaskCompletionSource<object>();
                }
                return ClosingTask.Task;
            }
        }

        public void CancelAll() {
            lock(this) {
                foreach(var c in CancellationTokenSources) {
                    c.Cancel();
                }
            }
        }
    }
}
