using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace DxxBrowser.driver {
    public class DxxDownloadingItem : DxxViewModelBase {
        const string STATUS_BEGIN = "Downloading...";
        const string STATUS_ERROR = "Error";
        const string STATUS_CANCELLED = "Cancelled";
        const string STATUS_COMPLETED = "Completed";

        public enum DownloadStatus {
            Downloading,
            Completed,
            Cancelled,
            Error,
        }

        private static Brush RunningColor = new SolidColorBrush(Color.FromRgb(0, 0, 255));
        private static Brush ErrorColor = new SolidColorBrush(Color.FromRgb(216, 0, 0));
        private static Brush CancelColor = new SolidColorBrush(Color.FromRgb(255, 128, 0));
        private static Brush CompletedColor = new SolidColorBrush(Color.FromRgb(0, 192, 0));


        private DownloadStatus mStatus;

        public string Url { get; }
        public string Description { get; }
        public string Name { get; }

        private int mPercent = -1;
        public int Percent {
            get => mPercent;
            set => setProp(callerName(), ref mPercent, value, "PercentString");
        }

        public DownloadStatus Status {
            get => mStatus;
            set => setProp(callerName(), ref mStatus, value, "StatusString", "StatusColor");
        }

        public string StatusString {
            get {
                switch(Status) {
                    default:
                    case DownloadStatus.Downloading:
                        return STATUS_BEGIN;
                    case DownloadStatus.Completed:
                        return STATUS_COMPLETED;
                    case DownloadStatus.Error:
                        return STATUS_ERROR;
                    case DownloadStatus.Cancelled:
                        return STATUS_CANCELLED;
                }
            }
        }

        public Brush StatusColor{
            get {
                switch (Status) {
                    default:
                    case DownloadStatus.Downloading:
                        return RunningColor;
                    case DownloadStatus.Completed:
                        return CompletedColor;
                    case DownloadStatus.Error:
                        return ErrorColor;
                    case DownloadStatus.Cancelled:
                        return CancelColor;
                }
            }
        }

        public string PercentString {
            get {
                if(Percent<0) {
                    return "-";
                }
                return $"{Math.Min(Percent,100)}%";
            }
        }

        public DxxDownloadingItem(DxxTargetInfo target) {
            Url = target.Url;
            Name = target.Name;
            Description = target.Description;
            Status = DownloadStatus.Downloading;
        }
    }

    public class DxxDownloader {
        HttpClient mHttpClient = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
        HashSet<string> mDownloading = new HashSet<string>();
        Dictionary<string, CancellationTokenSource> mCancellationTokens = new Dictionary<string, CancellationTokenSource>();
        bool mTerminated = false;
        public ObservableCollection<DxxDownloadingItem> DownloadingStateList { get; } = new ObservableCollection<DxxDownloadingItem>();


        public Subject<bool> Busy = new Subject<bool>();

        event Action AllDownloadCompleted;

        public static DxxDownloader Instance { get; } = new DxxDownloader();

        private WeakReference<DispatcherObject> mDispatherSource;
        private Dispatcher Dispatcher => mDispatherSource?.GetValue()?.Dispatcher;

        public void Initialize(DispatcherObject dispatcherSource) {
            mDispatherSource = new WeakReference<DispatcherObject>(dispatcherSource);
        }

        private DxxDownloader() {
            Busy.OnNext(false);
        }

        private CancellationTokenSource LockUrl(DxxTargetInfo target) {
            return Dispatcher.Invoke(() => {
                if (mTerminated) {
                    return null;
                }
                if (mDownloading.Contains(target.Url)) {
                    return null;
                }

                var ts = DownloadingStateList.Where((v) => v.Url == target.Url);
                if(Utils.IsNullOrEmpty(ts)) {
                    DownloadingStateList.Add(new DxxDownloadingItem(target));
                } else {
                    foreach (var t in ts) {
                        t.Status = DxxDownloadingItem.DownloadStatus.Downloading;
                    }
                }

                mDownloading.Add(target.Url);
                var cts = new CancellationTokenSource();
                mCancellationTokens.Add(target.Url, cts);
                Busy.OnNext(true);
                return cts;
            });
        }

        private void UnlockUrl(string url, DxxDownloadingItem.DownloadStatus result) {
            Dispatcher.Invoke(() => {
                mDownloading.Remove(url);
                mCancellationTokens.Remove(url);
                var ts = DownloadingStateList.Where((v) => v.Url == url);
                foreach (var t in ts) {
                    t.Status = result;
                }
                if (mDownloading.Count == 0) {
                    AllDownloadCompleted?.Invoke();
                    Busy.OnNext(false);
                }
            });
        }

        private void UpdateDownloadingProgress(string url, long received, long total) {
            int percent = -1;
            if(total>0) {
                percent = (int)Math.Round(100*(double)received / (double)total);
            }

            Dispatcher.Invoke(() => {
                var ts = DownloadingStateList.Where((v) => v.Url == url);
                if (!Utils.IsNullOrEmpty(ts)) {
                    foreach (var t in ts) {
                        t.Percent = percent;
                    }
                }
            });
        }

        public bool IsBusy {
            get {
                return Dispatcher.Invoke(() => {
                    return mDownloading.Count > 0;
                });
            }
        }

        public bool IsDownloading(string url) {
            return Dispatcher.Invoke(() => {
                return mDownloading.Contains(url);
            });
        }

        /**
         * アプリ終了時のダウンロード完了待ち
         * @param forceShutdown trueで呼ぶと、通信中のタスクをすべてキャンセルする。
         *                      false なら、通信中のタスクが完了するまで待つ。
         */
        public async Task TerminateAsync(bool forceShutdown) {
            var tc = new TaskCompletionSource<object>();
            Action completed = () => {
                tc.TrySetResult(null);
            };

            if(!Dispatcher.Invoke<bool>(() => {
                mTerminated = true;
                if (mDownloading.Count == 0 || AllDownloadCompleted != null) {
                    return false;
                }
                AllDownloadCompleted += completed;
                if (forceShutdown) {
                    foreach (var cts in mCancellationTokens) {
                        DxxLogger.Instance.Cancel(LOG_CAT, $"Cancelling: {DxxUrl.GetFileName(cts.Key)}");
                        cts.Value.Cancel();
                    }
                }
                return true;
            })) {
                // すべて終了済み
                return;
            }

            await tc.Task;
            AllDownloadCompleted -= completed;
        }

        public async Task CancelAllAsync() {
            var tc = new TaskCompletionSource<object>();
            Action completed = () => {
                tc.TrySetResult(null);
            };
            Dispatcher.Invoke(() => {
                if (mDownloading.Count == 0 || AllDownloadCompleted != null) {
                    return;
                }
                AllDownloadCompleted += completed;
                foreach (var cts in mCancellationTokens) {
                    DxxLogger.Instance.Cancel(LOG_CAT, $"Cancelling: {DxxUrl.GetFileName(cts.Key)}");
                    cts.Value.Cancel();
                }
            });
            await tc.Task;
            AllDownloadCompleted -= completed;
        }

        public void Cancel(string url) {
            if(string.IsNullOrEmpty(url)) {
                return;
            }
            Dispatcher.Invoke(() => {
                if (mCancellationTokens.TryGetValue(url, out var cts)) {
                    if (null != cts) {
                        DxxLogger.Instance.Cancel(LOG_CAT, $"Cancelling: {DxxUrl.GetFileName(url)}");
                        cts.Cancel();
                    }
                }
            });
        }

        const int BUFF_SIZE = 1024;

        const string LOG_CAT = "DL";

        public void Download(DxxTargetInfo target, string filePath, Action<bool> onCompleted=null) {
            var cts = LockUrl(target);
            if(null==cts) {
                onCompleted?.Invoke(false);
                return;
            }
            Task.Run(async () => {
                DxxDownloadingItem.DownloadStatus result = DxxDownloadingItem.DownloadStatus.Error;
                try {
                    var ct = cts.Token;
                    using (var response = await mHttpClient.GetAsync(target.Url, HttpCompletionOption.ResponseHeadersRead, ct)) {
                        if (response.StatusCode == HttpStatusCode.OK) {
                            using (var content = response.Content)
                            using (var stream = await content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                                long total = content.Headers.ContentLength ?? 0;
                                long recv = 0;
                                var buff = new byte[1024];
                                while(true) {
                                    try {
                                        ct.ThrowIfCancellationRequested();
                                        int len = await stream.ReadAsync(buff, 0, 1024, ct);
                                        if(len==0) {
                                            await fileStream.FlushAsync();
                                            break;
                                        }
                                        recv += len;
                                        if (total > 0) {
                                            UpdateDownloadingProgress(target.Url, recv, total);
                                        }
                                        await fileStream.WriteAsync(buff, 0, len);
                                        ct.ThrowIfCancellationRequested();
                                    } catch (Exception e) {
                                        if(e is OperationCanceledException) {
                                            result = DxxDownloadingItem.DownloadStatus.Cancelled;
                                        }
                                        throw e;
                                    }
                                }
                                //await stream.CopyToAsync(fileStream);
                                result = DxxDownloadingItem.DownloadStatus.Completed;
                                DxxLogger.Instance.Success(LOG_CAT, $"Completed: {target.Name}");
                            }
                        }
                    }
                } catch (Exception e) {
                    Debug.WriteLine(e.StackTrace);
                } finally {
                    if (result!=DxxDownloadingItem.DownloadStatus.Completed) {
                        if (result == DxxDownloadingItem.DownloadStatus.Cancelled) {
                            DxxLogger.Instance.Cancel(LOG_CAT, $"{result}: {target.Name}");
                        } else {
                            DxxLogger.Instance.Error(LOG_CAT, $"{result}: {target.Name}");
                        }
                        if (File.Exists(filePath)) {
                            File.Delete(filePath);
                        }
                    }
                    UnlockUrl(target.Url, result);
                }
                onCompleted?.Invoke(result==DxxDownloadingItem.DownloadStatus.Completed);
            });
        }
    }
}
