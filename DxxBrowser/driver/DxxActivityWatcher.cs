using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    /**
     * キャンセル可能なタスク（ダウンロードタスク）を実行し、その経過を監視するクラス
     */
    public class DxxActivityWatcher {
        #region Private Fields

        private bool Closing = false;
        private TaskCompletionSource<object> ClosingTask = null;
        private HashSet<CancellationTokenSource> CancellationTokenSources = new HashSet<CancellationTokenSource>();

        #endregion

        #region Singleton

        public static DxxActivityWatcher Instance { get; private set; }

        public static void Initialize() {
            Instance = new DxxActivityWatcher();
        }

        public static async Task TerminateAsync(bool cancelAll) {
            if (null != Instance) {
                await Instance?._TerminateAsync(cancelAll);
                Instance = null;
            }
        }

        private DxxActivityWatcher() {
        }

        public static bool IsBusy => Instance?._IsBusy ?? false;

        private bool _IsBusy {
            get {
                lock (this) {
                    return CancellationTokenSources.Count > 0;
                }
            }
        }
        #endregion

        /**
         * タスクを追加
         */
        private void Add(CancellationTokenSource cancellationTokenSource) {
            lock (this) {
                CancellationTokenSources.Add(cancellationTokenSource);
            }
        }

        /**
         * タスクを削除
         */
        private void Release(CancellationTokenSource cancellationTokenSource) {
            lock (this) {
                CancellationTokenSources.Remove(cancellationTokenSource);
                if (0 == CancellationTokenSources.Count) {
                    if (ClosingTask != null) {
                        ClosingTask.TrySetResult(null);
                    }
                }
            }
        }

        /**
         * 監視するタスクのプロシージャ型
         */
        public delegate Task<T> WatchingProc<T>(CancellationToken cancellationToken);

        /**
         * タスクの実行を開始する
         */
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

        /**
         * 後始末
         */
        private Task _TerminateAsync(bool cancelAll) {
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

        /**
         * すべてのタスクをキャンセルする
         */
        public void CancelAll() {
            lock(this) {
                foreach(var c in CancellationTokenSources) {
                    c.Cancel();
                }
            }
        }
    }
}
