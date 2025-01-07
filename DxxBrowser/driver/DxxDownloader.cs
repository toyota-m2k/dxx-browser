using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace DxxBrowser.driver {
    /**
     * ダウンロードアイテムの状態を保持するビューモデル
     */
    public class DxxDownloadingItem : MicViewModelBase {
        #region Constants

        const string STATUS_BEGIN = "Downloading...";
        const string STATUS_ERROR = "Error";
        const string STATUS_RETRYING = "Retrying";
        const string STATUS_CANCELLED = "Cancelled";
        const string STATUS_COMPLETED = "Completed";

        private static Brush RunningColor = new SolidColorBrush(Color.FromRgb(0, 0, 255));
        private static Brush ErrorColor = new SolidColorBrush(Color.FromRgb(216, 0, 0));
        private static Brush RetryingColor = new SolidColorBrush(Color.FromRgb(216, 0, 0));
        private static Brush CancelColor = new SolidColorBrush(Color.FromRgb(255, 128, 0));
        private static Brush CompletedColor = new SolidColorBrush(Color.FromRgb(0, 192, 0));

        /**
         * Downloading Status
         */
        public enum DownloadStatus {
            Downloading,
            Completed,
            Cancelled,
            Retrying,
            Error,
        }

        #endregion

        #region Properties

        public string Url { get; }
        public string Description { get; }
        public string Name { get; }

        private int mPercent = -1;
        public int Percent {
            get => mPercent;
            set => setProp(callerName(), ref mPercent, value, "PercentString");
        }

        private DownloadStatus mStatus;
        public DownloadStatus Status {
            get => mStatus;
            set => setProp(callerName(), ref mStatus, value, "StatusString", "StatusColor");
        }

        private string mErrorMsg = null;
        public void SetErrorMessage(string msg) {
            mErrorMsg = msg;
            if(null!=mErrorMsg) {
                mStatus = DownloadStatus.Error;
            }
            notify("StatusString");
            notify("StatusColor");
        }

        public string StatusString {
            get {
                switch (Status) {
                    default:
                    case DownloadStatus.Downloading:
                        return STATUS_BEGIN;
                    case DownloadStatus.Completed:
                        return STATUS_COMPLETED;
                    case DownloadStatus.Error:
                        if (string.IsNullOrWhiteSpace(mErrorMsg)) {
                            return STATUS_ERROR;
                        } else {
                            return $"{STATUS_ERROR} ({mErrorMsg})";
                        }
                    case DownloadStatus.Retrying:
                        return STATUS_RETRYING;
                    case DownloadStatus.Cancelled:
                        return STATUS_CANCELLED;
                }
            }
        }

        public Brush StatusColor {
            get {
                switch (Status) {
                    default:
                    case DownloadStatus.Downloading:
                        return RunningColor;
                    case DownloadStatus.Completed:
                        return CompletedColor;
                    case DownloadStatus.Error:
                        return ErrorColor;
                    case DownloadStatus.Retrying:
                        return RetryingColor;
                    case DownloadStatus.Cancelled:
                        return CancelColor;
                }
            }
        }

        public string PercentString {
            get {
                if (Percent < 0) {
                    return "-";
                }
                return $"{Math.Min(Percent, 100)}%";
            }
        }
        #endregion

        public DxxDownloadingItem(DxxTargetInfo target) {
            Url = target.Url;
            Name = target.Name;
            Description = target.Description;
            Status = DownloadStatus.Downloading;
        }
    }

    /**
     * ダウンローダークラス
     */
    public class DxxDownloader {
        /**
         * ダウンロードタスク情報
         */
        class DLTask {
            public DxxDownloadingItem ItemInfo { get; }
            public string FilePath { get; }
            public int MaxRetry { get; }
            public int Retry { get; set; }
            public Action<DxxDownloadingItem.DownloadStatus> OnCompleted { get; }
            public CancellationTokenSource Cancellation { get; set; } = null;

            public DLTask(DxxDownloadingItem item, string path, int maxRetry, Action<DxxDownloadingItem.DownloadStatus> onCompleted) {
                ItemInfo = item;
                FilePath = path;
                Retry = 0;
                MaxRetry = maxRetry;
                OnCompleted = onCompleted;
            }
        }

        #region Singleton

        public static DxxDownloader Instance { get; private set; }

        public static void Initialize(DispatcherObject dispatcherSource) {
            Instance = new DxxDownloader(dispatcherSource);
        }

        public static void RunOnUIThread(Action action) {
            if (Instance == null) {
                return;
            }
            Instance.Dispatcher.Invoke(action);
        }

        public static async Task TerminateAsync() {
            await Instance.AbortAsync();
            Instance = null;
        }

        public static bool IsBusy => Instance?.HasTasks ?? false;

        private DxxDownloader(DispatcherObject dispatcherSource) {
            mDispatherSource = new WeakReference<DispatcherObject>(dispatcherSource);
            Busy.OnNext(false);
        }

        #endregion

        #region Public Properties

        /**
         * タスクの状態リスト（このままリストビューに表示する）
         */
        public ObservableCollection<DxxDownloadingItem> DownloadingStateList { get; } = new ObservableCollection<DxxDownloadingItem>();
        
        /**
         * ビジー（ダウンロード中）状態の監視用
         */
        public Subject<bool> Busy = new Subject<bool>();

        #endregion

        #region Public API's

        /**
         * ダウンロード予約
         * @param target    ダウンロードするURLを持ったオブジェクト
         * @param filePath  保存ファイルパス
         * @param maxRetry  最大リトライ回数
         * @param onCompleted ダウンロード完了時（成功・失敗に関係ない）に実行するコールバック
         */
        public bool Reserve(DxxTargetInfo target, string filePath, int maxRetry, Action<DxxDownloadingItem.DownloadStatus> onCompleted) {
            if (!Dispatcher.CheckAccess()) {
                Debug.Assert(false, "Reserve: must be called in ui-thread.");
                throw new Exception("Invalid Thread");
            }

            if (Finalizing) {
                return false;
            }
            if (AllTasks.Contains(target.Url)) {
                return false;
            }
            AllTasks.Add(target.Url);
            if (AllTasks.Count == 1) {
                Busy.OnNext(true);
            }

            var item = FindDownloadingItem(target.Url);
            if (item == null) {
                item = new DxxDownloadingItem(target);
                DownloadingStateList.Add(item);
            }
            var task = new DLTask(item, filePath, maxRetry, onCompleted);
            Queue.Enqueue(task);
            Trigger();
            return true;
        }

        /**
         * URLはダウンロード中か？
         * （通信中だけでなく、ダウンロード待ちキューに入っているものもダウンロード中として扱われる。）
         */
        public bool IsDownloading(string url) {
            return Dispatcher.Invoke(() => {
                return AllTasks.Contains(url);
            });
        }

        /**
         * ダウンロードを中止
         * @param url 中止するURL
         */
        public void Cancel(string url) {
            if (!Dispatcher.CheckAccess()) {
                Debug.Assert(false, "Cancel: must be called in ui-thread.");
                throw new Exception("Invalid Thread");
            }

            if (AllTasks.Count == 0 || !AllTasks.Contains(url)) {
                return;
            }
            var qt = Queue.Where((v) => v.ItemInfo.Url == url);
            if (Utils.IsNullOrEmpty(qt)) {
                Queue = new Queue<DLTask>(Queue.Where((v) => v.ItemInfo.Url != url));
                AllTasks.Remove(url);
                if (AllTasks.Count == 0) {
                    Busy.OnNext(false);
                }

            } else {
                var t = ActiveTasks.Find((v) => v.ItemInfo.Url == url);
                if (t != null) {
                    t.Cancellation?.Cancel();
                }
            }
        }

        /**
         * すべてのダウンロードを中止
         * 通信の停止を待たない。通信中かどうかは、IsBusy でチェックするか、Busy をSubscribeして状態変化を監視する。
         */
        public void CancelAll() {
            if (!Dispatcher.CheckAccess()) {
                Debug.Assert(false, "CancelAll: must be called in ui-thread.");
                throw new Exception("Invalid Thread");
            }

            if (AllTasks.Count == 0) {
                return;
            }

            while (Queue.Count > 0) {
                DLTask task = Queue.Dequeue();
                AllTasks.Remove(task.ItemInfo.Url);
            }
            foreach (var t in ActiveTasks) {
                t.Cancellation.Cancel();
            }
            if (AllTasks.Count == 0) {
                Busy.OnNext(false);
            }
        }

        #endregion

        #region Constants

        private const int CONCURRENT_TASK = 2;
        private const int BUFF_SIZE = 1024*1024;    // 1MB
        private const string LOG_CAT = "DL";
        public const int MAX_RETRY = 2;

        #endregion

        #region Private Fields

        private bool Finalizing = false;

        private Queue<DLTask> Queue = new Queue<DLTask>(128);
        private List<DLTask> ActiveTasks = new List<DLTask>(CONCURRENT_TASK);
        private HashSet<string> AllTasks = new HashSet<string>();

        private WeakReference<DispatcherObject> mDispatherSource;
        private Dispatcher Dispatcher => mDispatherSource?.GetValue()?.Dispatcher;

        private HttpClient mHttpClient = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };

        #endregion

        #region Private Methods

        /**
         * 完全終了
         * すべてのタスク終了するまで待機する。
         */
        private async Task AbortAsync() {
            if (mHttpClient == null) {
                return;
            }
            if (!Dispatcher.CheckAccess()) {
                Debug.Assert(false, "AbortAsync: must be called in ui-thread.");
                throw new Exception("Invalid Thread");
            }

            Finalizing = true;
            if (AllTasks.Count == 0) {
                return;
            }
            while (Queue.Count > 0) {
                DLTask task = Queue.Dequeue();
                AllTasks.Remove(task.ItemInfo.Url);
            }
            if (AllTasks.Count == 0) {
                return;
            }
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            using (Busy.Subscribe((v) => { if (!v) tcs.TrySetResult(null); })) {
                foreach (var t in ActiveTasks) {
                    t.Cancellation.Cancel();
                }
                await tcs.Task;
            }
            mHttpClient?.Dispose();
            mHttpClient = null;
            return;
        }

        /**
         * １つ以上のタスクが実行中か？
         */
        private bool HasTasks => AllTasks.Count > 0;

        /**
         * （可能なら）次のタスクを開始する
         */
        private void Trigger() {
            Dispatcher.InvokeAsync(() => {
                if (!Finalizing && ActiveTasks.Count < CONCURRENT_TASK && Queue.Count > 0) {
                    var dlTask = Queue.Dequeue();
                    ActiveTasks.Add(dlTask);
                    DownloadOne(dlTask);
                }
            });
        }

        /**
         * （使いまわしのため）URLが一致する、DxxDownloadingItemを取得する。
         */
        private DxxDownloadingItem FindDownloadingItem(string url) {
            var ts = DownloadingStateList.Where((v) => v.Url == url);
            if (!Utils.IsNullOrEmpty(ts)) {
                return ts.First();
            }
            return null;
        }

        /**
         * ダウンロードの進捗情報を更新する
         */
        private void UpdateProgress(DLTask dlTask, long received, long total) {
            int percent = -1;
            if (total > 0) {
                percent = (int)Math.Round(100 * (double)received / (double)total);
            }
            if (percent == 100 || percent - dlTask.ItemInfo.Percent > 2) {
                Dispatcher.InvokeAsync(() => {
                    dlTask.ItemInfo.Percent = percent;
                });
            }
        }

        /**
         * ダウンロード状態を更新する。
         */
        private void UpdateStatus(DLTask dlTask, DxxDownloadingItem.DownloadStatus status, string errorMsg=null) {
            Dispatcher.InvokeAsync(() => {
                dlTask.ItemInfo.Status = status;
                if(status==DxxDownloadingItem.DownloadStatus.Error && errorMsg!=null) {
                    dlTask.ItemInfo.SetErrorMessage(errorMsg);
                }
            });
        }

        /**
         * １つのタスクのダウンロードを実行する。
         */
        private void DownloadOne(DLTask dlTask) {
            Task.Run(async () => {
                DxxDownloadingItem.DownloadStatus result = DxxDownloadingItem.DownloadStatus.Error;
                using (var cts = new CancellationTokenSource()) {
                    UpdateStatus(dlTask, DxxDownloadingItem.DownloadStatus.Downloading);
                    dlTask.Cancellation = cts;
                    var ct = dlTask.Cancellation.Token;
                    var filePath = dlTask.FilePath;
                    try {
                        using (var response = await mHttpClient.GetAsync(dlTask.ItemInfo.Url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                            if (response.StatusCode == HttpStatusCode.OK) {
                                using (var content = response.Content)
                                using (var stream = await content.ReadAsStreamAsync())
                                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                                    long total = content.Headers.ContentLength ?? 0;
                                    long recv = 0;
                                    var buff = new byte[BUFF_SIZE];
                                    while (true) {
                                        try {
                                            ct.ThrowIfCancellationRequested();
                                            int len = await stream.ReadAsync(buff, 0, BUFF_SIZE, ct);
                                            if (len == 0) {
                                                await fileStream.FlushAsync();
                                                break;
                                            }
                                            recv += len;
                                            if (total > 0) {
                                                UpdateProgress(dlTask, recv, total);
                                            }
                                            await fileStream.WriteAsync(buff, 0, len);
                                            ct.ThrowIfCancellationRequested();
                                        } catch (Exception e) {
                                            if (e is OperationCanceledException) {
                                                // キャンセル
                                                result = DxxDownloadingItem.DownloadStatus.Cancelled;
                                                UpdateStatus(dlTask, result);
                                                DxxLogger.Instance.Cancel(LOG_CAT, $"{result}: {dlTask.ItemInfo.Name}:{dlTask.ItemInfo.Description}");
                                            } else {
                                                // キャンセル以外のエラー
                                                Debug.WriteLine(e.StackTrace);
                                                result = DxxDownloadingItem.DownloadStatus.Error;
                                                UpdateStatus(dlTask, result, e.Message);
                                                DxxLogger.Instance.Error(LOG_CAT, $"{result}: {e.Message} ({dlTask.ItemInfo.Name}:{dlTask.ItemInfo.Description})");
                                            }
                                            throw e;
                                        }
                                    }
                                    //await stream.CopyToAsync(fileStream);
                                    result = DxxDownloadingItem.DownloadStatus.Completed;
                                    UpdateStatus(dlTask, result);
                                    DxxLogger.Instance.Success(LOG_CAT, $"{result}: {dlTask.ItemInfo.Name}:{dlTask.ItemInfo.Description}");
                                }
                            } else {
                                // HTTP のエラー
                                result = DxxDownloadingItem.DownloadStatus.Error;
                                UpdateStatus(dlTask, result, $"{(int)response.StatusCode} {response.ReasonPhrase}");
                                DxxLogger.Instance.Error(LOG_CAT, $"{result}: {(int)response.StatusCode} {response.ReasonPhrase} ({dlTask.ItemInfo.Name}:{dlTask.ItemInfo.Description})");
                            }
                        }
                    } catch (Exception e) {
                        DxxLogger.Instance.Error(LOG_CAT, $"{e.Message}: {dlTask.ItemInfo.Url}:{dlTask.ItemInfo.Description}");
                        Debug.WriteLine(e.StackTrace);
                    } finally {
                        if (result != DxxDownloadingItem.DownloadStatus.Completed) {
                            DxxLogger.Instance.Error(LOG_CAT, $"{result}: {dlTask.ItemInfo.Url}:{dlTask.ItemInfo.Description}");
                        }
                        Completion(dlTask, result);
                        Trigger();
                    }
                }
            });
        }

        /**
         * ダウンロードが完了したときの処理
         */
        private void Completion(DLTask dlTask, DxxDownloadingItem.DownloadStatus result) {
            dlTask.Cancellation = null;
            if (result != DxxDownloadingItem.DownloadStatus.Completed) {
                // 成功しなかった場合は、（中途半端な）保存ファイルを削除する。
                if (File.Exists(dlTask.FilePath)) {
                    File.Delete(dlTask.FilePath);
                }
            }

            Dispatcher.Invoke(() => {
                if (result == DxxDownloadingItem.DownloadStatus.Error) {
                    // キャンセル以外のエラーの場合はリトライ
                    if (dlTask.Retry <= dlTask.MaxRetry) {
                        if (!Finalizing) {
                            dlTask.Retry++;
                            dlTask.ItemInfo.Percent = -1;
                            ActiveTasks.Remove(dlTask);
                            Queue.Enqueue(dlTask);
                            dlTask.ItemInfo.Status = DxxDownloadingItem.DownloadStatus.Retrying;
                            return;
                        } else {
                            // 終了処理中でリトライできなかったときは、キャンセル扱い
                            result = DxxDownloadingItem.DownloadStatus.Cancelled;
                        }
                    }
                }
                // ここから完了時の処理
                dlTask.OnCompleted?.Invoke(result);
                ActiveTasks.Remove(dlTask);
                AllTasks.Remove(dlTask.ItemInfo.Url);
                if (AllTasks.Count == 0) {
                    Busy.OnNext(false);
                }
            });
        }

        #endregion
    }
}
