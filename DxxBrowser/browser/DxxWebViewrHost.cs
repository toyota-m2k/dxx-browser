using Common;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DxxBrowser {
#pragma warning disable CS0618 // 型またはメンバーが古い形式です
    public class DxxWebViewHost : MicViewModelBase {
        #region Properties

        public enum ErrorLevel {
            NONE,
            ERROR,
            FATAL_ERROR,
        }

        public bool IsMain { get; }

        [Disposal(false)]
        private WeakReference<WebView2> mBrowser;
        public WebView2 Browser => mBrowser?.GetValue();
        private WeakReference<Window> mOwner;
        public Window Owner => mOwner?.GetValue();

        public ReactiveProperty<DxxBookmark> Bookmarks { get; }
        [Disposal(false)]
        public ReactiveProperty<ObservableCollection<DxxTargetInfo>> TargetList { get; }

        public ReactiveProperty<string> Url { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> Loading { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> IsBookmarked { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> HasFrameLink { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowFrameList { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<IDxxDriver> Driver { get; } = new ReactiveProperty<IDxxDriver>(DxxDriverManager.NOP);
        public ReactiveProperty<bool> IsTarget { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainerList { get; } = new ReactiveProperty<bool>(false);
        public ReadOnlyReactiveProperty<bool> IsDownloadable { get; private set; }

        public ReactiveProperty<ErrorLevel> HasError{ get; } = new ReactiveProperty<ErrorLevel>(ErrorLevel.NONE);
        public ReactiveProperty<string> CurrentError { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<ObservableCollection<string>> FrameUrls { get; } = new ReactiveProperty<ObservableCollection<string>>(new ObservableCollection<string>());
        //public ReactiveProperty<string> ActivatedUrl { get; } = new ReactiveProperty<string>();
        //public ReactiveProperty<bool> LinkActivated { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<string> StatusLine { get; } = new ReactiveProperty<string>("Ready");

        private void InitializeProperties() {
            IsDownloadable = IsContainer.CombineLatest(IsTarget, (c, t) => {
                return c || t;
            }).ToReadOnlyReactiveProperty();

            Url.Subscribe((v) => {
                var driver = DxxDriverManager.Instance.FindDriver(v);
                if (driver != null) {
                    Driver.Value = driver;
                    Uri uri = new Uri(v);
                    var dxxUrl = new DxxUrl(uri, driver, driver.GetNameFromUri(uri, "main"), "");
                    IsTarget.Value = driver.LinkExtractor.IsTarget(dxxUrl);
                    IsContainer.Value = driver.LinkExtractor.IsContainer(dxxUrl);
                    IsContainerList.Value = driver.LinkExtractor.IsContainerList(dxxUrl);
                } else {
                    Driver.Value = DxxDriverManager.NOP;
                    IsTarget.Value = false;
                    IsContainer.Value = false;
                    IsContainerList.Value = false;
                }
                IsBookmarked.Value = Bookmarks.Value.FindBookmark(v)!=null;
            });


        }

        #endregion

        #region Commands

        public ReactiveCommand GoBackCommand { get; } = new ReactiveCommand();
        public ReactiveCommand GoForwardCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ReloadCommand { get; } = new ReactiveCommand();
        public ReactiveCommand StopCommand { get; } = new ReactiveCommand();
        public ReactiveCommand RepeatActionCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<string> BookmarkCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand<string> NavigateCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand ClearURLCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<string> AnalyzeCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand DownloadCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ListingCommand { get; } = new ReactiveCommand();
        public ReactiveCommand SetupDriverCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<string> CopyCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand<string> FrameSelectCommand { get; } = new ReactiveCommand<string>();
        public Subject<string> LinkedResources { get; } = new Subject<string>();

        //private DxxUrl CreateDxxUrl() {
        //    var driver = Driver.Value;
        //    var uri = new Uri(Url.Value);
        //    return new DxxUrl(uri, driver, driver.GetNameFromUri(uri, "link"), IsMain ? "from main" : "from sub");
        //}

        private void InitializeCommands() {
            GoBackCommand.Subscribe(() => {
                GoBack();
            });
            GoForwardCommand.Subscribe(() => {
                GoForward();
            });
            ReloadCommand.Subscribe(() => {
                Reload();
            });
            StopCommand.Subscribe(() => {
                Stop();
            });
            BookmarkCommand.Subscribe((v) => {
                if (string.IsNullOrEmpty(v)||!v.StartsWith("http")) {
                    IsBookmarked.Value = false;
                    return;
                }
                if (IsBookmarked.Value) {
                    Bookmarks.Value.AddBookmark("", v);
                } else {
                    Bookmarks.Value.RemoveBookmark(v);
                    Url.Value = v;
                }
            });
            NavigateCommand.Subscribe((v) => {
                Navigate(v);
            });
            ClearURLCommand.Subscribe((v) => {
                Url.Value = "";
            });
            AnalyzeCommand.Subscribe(async (v) => {
                if(v==null) {
                    v = Url.Value;
                }
                var aw = new DxxAnalysisWindow(v, await GetCurrentHtmlAsync());
                //aw.Owner = Owner;
                aw.Show();
            });
            CopyCommand.Subscribe((v) => {
                Clipboard.SetData(DataFormats.Text, v);
            });
            DownloadCommand.Subscribe(() => {
                if(IsTarget.Value || IsContainer.Value) {
                    DxxDriverManager.Instance.Download(Url.Value, null, "");
                }
            });
            ListingCommand.Subscribe(async () => {
                if (IsContainerList.Value) {
                    var uri = new Uri(Url.Value);
                    var dxxUrl = new DxxUrl(uri, Driver.Value, Driver.Value.GetNameFromUri(uri), "");
                    var targets = await Driver.Value.LinkExtractor.ExtractContainerList(dxxUrl, await GetCurrentHtmlAsync());
                    if (targets != null && targets.Count > 0) {
                        TargetList.Value = new ObservableCollection<DxxTargetInfo>(targets);
                    } else {
                        TargetList.Value?.Clear();
                    }
                }
            });

            SetupDriverCommand.Subscribe(() => {
                DxxDriverManager.Instance.Setup(Driver.Value, Owner);
            });

            FrameSelectCommand.Subscribe((v) => {
                if(IsMain) {
                    RequestLoadInSubview.OnNext(v);
                } else {
                    Navigate(v);
                }
            });

            LinkedResources.Subscribe((v) => {
                var driver = DxxDriverManager.Instance.FindDriver(v);
                if (driver != null) {
                    driver.HandleLinkedResource(v, Url.Value, Browser.CoreWebView2.DocumentTitle);
                }
            });
        }
        #endregion

        #region Events
        public Subject<DxxUrl> MainViewBeginAutoDownload { get; } = new Subject<DxxUrl>();
        public Subject<string> RequestLoadInSubview { get; } = new Subject<string>();
        public Subject<string> RequestLoadInMainView { get; } = new Subject<string>();
        #endregion

        #region Construction / Disposition

        public DxxWebViewHost(bool isMain, Window owner, DxxBookmark bookmarks,
            ReactiveProperty<ObservableCollection<DxxTargetInfo>> targetList) {
            IsMain = isMain;
            Bookmarks = new ReactiveProperty<DxxBookmark>(bookmarks);
            TargetList = targetList;
            mOwner = new WeakReference<Window>(owner);
            mBrowser = null;
            InitializeProperties();
            InitializeCommands();
            //LMonitor = new LoadingMonitor(this);
        }

        public void SetBrowser(WebView2 wv) { 
            mBrowser = new WeakReference<WebView2>(wv);
            wv.NavigationStarting += WebView_NavigationStarting;
            wv.NavigationCompleted += WebView_NavigationCompleted;
            wv.ContentLoading += WebView_ContentLoading;
            wv.CoreWebView2.DOMContentLoaded += WebView_DOMContentLoaded;
            wv.CoreWebView2.FrameNavigationCompleted += WebView_FrameNavigationCompleted;
            wv.CoreWebView2.FrameNavigationStarting += WebView_FrameNavigationStarting;
            wv.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
            wv.CoreWebView2.PermissionRequested += WebView_PermissionRequired;
            wv.CoreWebView2.ProcessFailed += WebView_ProcessFailed;
            wv.CoreWebView2.HistoryChanged += WebView_HistoryChanged;
            wv.CoreWebView2.DocumentTitleChanged += WebView_DocumentTitleChanged;

            wv.SourceUpdated += WebView_SourceUpdated;
            wv.SourceChanged += WebView_SourceChanged;
            wv.WebMessageReceived += WebView_WebMessageReceived;
            wv.CoreWebView2.WebResourceRequested += WebView_WebResourceRequested;
            wv.CoreWebView2.WebResourceResponseReceived += WebView_WebResourceResponseReceived;


            //wv.CoreWebView2.FrameContentLoading += WebView_FrameContentLoading;
            //wv.CoreWebView2.FrameDOMContentLoaded += WebView_FrameDOMContentLoaded;
            //wv.UnsafeContentWarningDisplaying += WebView_UnsafeContentWarningDisplaying;
            //wv.UnsupportedUriSchemeIdentified += WebView_UnsupportedUriSchemeIdentified;
            //wv.UnviewableContentIdentified += WebView_UnviewableContentIdentified;
            //wv.ScriptNotify += WebView_ScriptNotify;
            //wv.LongRunningScriptDetected += WebView_LongRunningScriptDetected;
            //wv.MoveFocusRequested += WebView_MoveFocusRequested;
            //wv.Process.ProcessExited += WebView_ProcessExited;
        }

        public void ResetBrowser() {
            WebView2 wv = Browser;
            if (null == wv) {
                return;
            }
            wv.NavigationStarting -= WebView_NavigationStarting;
            wv.NavigationCompleted -= WebView_NavigationCompleted;
            wv.ContentLoading -= WebView_ContentLoading;
            wv.CoreWebView2.DOMContentLoaded -= WebView_DOMContentLoaded;
            wv.CoreWebView2.FrameNavigationCompleted -= WebView_FrameNavigationCompleted;
            wv.CoreWebView2.FrameNavigationStarting -= WebView_FrameNavigationStarting;
            wv.CoreWebView2.NewWindowRequested -= WebView_NewWindowRequested;
            wv.CoreWebView2.PermissionRequested -= WebView_PermissionRequired;
            wv.CoreWebView2.ProcessFailed -= WebView_ProcessFailed;
            mBrowser = null;
        }

        public override void Dispose() {
            var wv = Browser;
            ResetBrowser();
            //if(null!=wv) {
            //    wv.Dispose();
            //}
            base.Dispose();
        }

        #endregion

        #region Navigation

        //struct Pending {
        //    public enum Command {
        //        None,
        //        Load,
        //        GoBack,
        //        GoForward,
        //        Reload,
        //    }
        //    public Command Waiting;
        //    public string Url;
        //    public Pending(Command cmd, string url=null) {
        //        Waiting = Command.None;
        //        Url = null;
        //    }
        //}
        //Pending PendingCommand = new Pending(Pending.Command.None);

        void Navigate(string url) {
            var browser = Browser;
            var uri = DxxUrl.FixUpUrl(url);
            if (null==uri || null==browser) { 
                return;
            }
            if (url == browser.Source.ToString()) {
                Reload();
            }
            else {
                browser.Source = new Uri(url);
            }
        }

        void Stop() {
            var browser = Browser;
            if (null == browser) {
                return;
            }
            //LMonitor.Renew();
            browser?.Stop();
        }

        void Reload() {
            Browser?.Reload();
        }

        void GoBack() {
            Browser?.GoBack();
        }

        void GoForward() {
            Browser?.GoForward();
        }

        void UpdateHistory() {
            var browser = Browser;
            if (null == browser) {
                HasPrev.Value = false;
                HasNext.Value = false;
            } else {
                HasPrev.Value = Browser.CanGoBack;
                HasNext.Value = Browser.CanGoForward;
            }
        }

        //class LoadingMonitor {
        //    private WeakReference<DxxWebViewHost> mViewModel;
        //    private DxxWebViewHost ViewModel => mViewModel?.GetValue();

        //    public LoadingMonitor(DxxWebViewHost viewModel) {
        //        mViewModel = new WeakReference<DxxWebViewHost>(viewModel);
        //    }
        //    class Info {
        //        ulong Generation;
        //        public string Url;
        //        public bool Frame;

        //        public Info(ulong gene, string url, bool frame) {
        //            Generation = gene;
        //            Url = url;
        //            Frame = frame;
        //        }

        //        public bool IsSame(string url, bool frame) {
        //            return url == Url && frame == Frame;
        //        }
        //    }

        //    ulong Generation = 0;
        //    static IEnumerable<Info> EMPTY = new Info[0];
        //    IEnumerable<Info> Loadings = EMPTY;

        //    IEnumerable<Info> One(Info info) {
        //        yield return info;
        //    }

        //    public void Renew() {
        //        Generation++;
        //        Loadings = EMPTY;
        //        ViewModel.Loading.Value = false;
        //        ViewModel.StatusLine.Value = "";
        //    }

        //    public void OnStartLoading(string url, bool frame) {
        //        Loadings = Loadings.Concat(One(new Info(Generation, url, frame)));
        //        // 挙動から推測して、
        //        // ドキュメントのロードが完了してから、Frameのロードが始まり、その場合、NavigationCompletedイベントは発行されないようだ。
        //        // なので、frame の Loadが開始されるタイミングで、NavigationCompletedを受け取ったものとしてみる。
        //        if (frame) {
        //            Loadings = Loadings.Where((v) => { return v.Frame; });
        //        }
        //        ViewModel.Loading.Value = !Utils.IsNullOrEmpty(Loadings);
        //        ViewModel.StatusLine.Value = $"Loading> {url}";
        //    }

        //    public void OnEndLoading(string url, bool frame) {
        //        Loadings = Loadings.Where((v) => !v.IsSame(url, frame)) ?? EMPTY;
        //        ViewModel.Loading.Value = !Utils.IsNullOrEmpty(Loadings);
        //        ResumeStatusLine();
        //    }

        //    public void ResumeStatusLine() {
        //        if (Utils.IsNullOrEmpty(Loadings)) {
        //            ViewModel.StatusLine.Value = "Ready";
        //        } else {
        //            var last = Loadings.Last()?.Url;
        //            ViewModel.StatusLine.Value = (string.IsNullOrEmpty(last)) ? "Ready" : $"Loading> {last}";
        //        }
        //    }
        //}

        //private LoadingMonitor LMonitor;


        /**
         * 現在表示中のHTMLを取得する
         */
        public async Task<string> GetCurrentHtmlAsync() {
            // JavaScriptを実行してページのHTMLを取得
            string html = await Browser.ExecuteScriptAsync("document.documentElement.outerHTML");

            // ExecuteScriptAsyncは結果をJSON文字列として返すため、不要な引用符を削除
            html = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(html);
            return html;
        }

        #endregion

        #region Frame List

        void ClearFrameList() {
            FrameUrls.Value.Clear();
            HasFrameLink.Value = false;
            ShowFrameList.Value = false;
        }

        void AddFrameList(string url) {
            FrameUrls.Value.Remove(url);
            FrameUrls.Value.Insert(0, url);
            HasFrameLink.Value = FrameUrls.Value.Count > 0;
            ShowFrameList.Value = HasFrameLink.Value;
        }

        #endregion

        #region Navigation Events

        /**
         * NavigationID と Urlマップ
         * WebView は、各イベントのEventArgsに Uri 属性が渡ってきていたが、
         * WebView2では、NavigationStartingの時に発行される NavigationId という ulong 値で管理するようになったようだ。
         */
        class NavigationMap {
            Dictionary<ulong, Uri> map = new Dictionary<ulong, Uri>();
            public void Register(ulong id, Uri uri) {
                map[id] = uri;
            }
            public void Unregister(ulong id) {
                map.Remove(id);
            }
            public void Clear() {
                map.Clear();
            }
            public Uri GetUri(ulong id) {
                return map.GetValue(id);
            }
            public Uri this[ulong id] => GetUri(id);
        }
        private NavigationMap navMap = new NavigationMap();

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
            Debug.WriteLine($"{callerName()}: {e.Uri} ({e.NavigationId})");
            var leftCtrl = Keyboard.IsKeyDown(Key.LeftCtrl);
            if (IsMain) {
                if (leftCtrl) {
                    // WebView2 ではここには入ってこないはず。
                    RequestLoadInSubview.OnNext(e.Uri);
                    e.Cancel = true;
                    return;
                }
            }
            var uri = new Uri(e.Uri);
            navMap.Register(e.NavigationId, uri);

            if (!leftCtrl) {
                var driver = DxxDriverManager.Instance.FindDriver(e.Uri);
                if (driver != null) {
                    var du = new DxxUrl(e.Uri, driver, driver.GetNameFromUri(uri, "link"), "");
                    if (du.IsContainer || du.IsTarget) {
                        if (IsMain) {
                            MainViewBeginAutoDownload.OnNext(du);
                        }
                        DxxDriverManager.Instance.Download(e.Uri, null, "");
                        e.Cancel = true;
                        return;
                    }
                }
            }
            Loading.Value = true;
            //LMonitor.Renew();
            //UpdateHistory();
        }

        private void WebView_ContentLoading(object sender, CoreWebView2ContentLoadingEventArgs e) {
            Debug.WriteLine(callerName());
            var uri = navMap[e.NavigationId];
            if (uri != null) {
                Url.Value = uri.ToString();
                //LMonitor.OnStartLoading(uri.ToString(), false);
            }
            if (HasError.Value == ErrorLevel.ERROR) {
                HasError.Value = ErrorLevel.NONE;
            }
            ClearFrameList();
            //UpdateHistory();
        }

        private void WebView_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e) {
            Debug.WriteLine(callerName());
            //UpdateHistory();
            //var script = @"
            //        var els = document.getElementsByTagName('a');
            //        Array.prototype.map.call(els, (v) => {
            //            v.onmouseover = function(e) {
            //                window.external.notify('i=' + e.currentTarget.href);
            //            }
            //            v.onmouseleave = function(e) {
            //                window.external.notify('o=' + e.currentTarget.href);
            //            }
                        
            //        })";
            //_ = Browser.ExecuteScriptAsync("eval", new string[] { script });
            //_ = Browser.InvokeScriptAsync("eval", new string[] { "document.onmousemove = function(e) { window.external.notify(e); }" });
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            Debug.WriteLine(callerName());
            var uri = navMap[e.NavigationId];
            if (uri != null) {
                navMap.Unregister(e.NavigationId);
                //LMonitor.OnEndLoading(uri.ToString(), false);
                //UpdateHistory();
                Loading.Value = true;
            }
        }

        #endregion

        #region Frame Loading Events


        private void WebView_FrameNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
            navMap.Register(e.NavigationId, new Uri(e.Uri));
            Debug.WriteLine($"{callerName()}:{e.Uri}");
            if(e.Uri=="about:blank"||e.Uri.StartsWith("javascript:")) {
                //e.Cancel = true;
                return;
            }
            //UpdateHistory();
            if (HasError.Value == ErrorLevel.ERROR) {
                HasError.Value = ErrorLevel.NONE;
            } else {
                //LMonitor.OnStartLoading(e.Uri, true);
                AddFrameList(e.Uri);
            }
            StatusLine.Value = $"Loading: {e.Uri}";
        }

        //private void WebView_FrameContentLoading(object sender, WebViewControlContentLoadingEventArgs e) {
        //    Debug.WriteLine(callerName());
        //    LMonitor.OnStartLoading(e.Uri.ToString(), true);
        //    AddFrameList(e.Uri.ToString());
        //    UpdateHistory();
        //}


        //private void WebView_FrameDOMContentLoaded(object sender, WebViewControlDOMContentLoadedEventArgs e) {
        //    Debug.WriteLine(callerName());
        //    UpdateHistory();
        //}

        private void WebView_FrameNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            Debug.WriteLine(callerName());
            var uri = navMap[e.NavigationId];
            if (uri != null) {
                navMap.Unregister(e.NavigationId);
                Debug.WriteLine($"{callerName()}:{uri}");
                Debug.WriteLine(callerName());
                //LMonitor.OnEndLoading(uri.ToString(), true);
                //UpdateHistory();
            }
            StatusLine.Value = "Ready";
        }
        #endregion

        #region Special Caces


        private void WebView_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e) {
            Debug.WriteLine(callerName());
            if(IsMain) {
                // Main View の場合は、サブビューに表示
                RequestLoadInSubview.OnNext(e.Uri.ToString());
            } else {
                // サブビューの場合は、自分自身に表示
                Navigate(e.Uri.ToString());
            }
            e.Handled = true;
        }


        private void WebView_PermissionRequired(object sender, CoreWebView2PermissionRequestedEventArgs e) {
            Debug.WriteLine(callerName());
            e.State = CoreWebView2PermissionState.Deny;
        }

        #endregion

        #region Potential Probrems
        //private void WebView_LongRunningScriptDetected(object sender, WebViewControlLongRunningScriptDetectedEventArgs e) {
        //    Debug.WriteLine(callerName());
        //    e.StopPageScriptExecution = true;
        //    HasError.Value = ErrorLevel.ERROR;
        //    CurrentError.Value = "Long running script was detected and stopped it.";
        //}

        //private void WebView_UnviewableContentIdentified(object sender, WebViewControlUnviewableContentIdentifiedEventArgs e) {
        //    Debug.WriteLine(callerName());
        //    HasError.Value = ErrorLevel.ERROR;
        //    CurrentError.Value = "content in unknown type was received.";
        //}

        //private void WebView_UnsupportedUriSchemeIdentified(object sender, WebViewControlUnsupportedUriSchemeIdentifiedEventArgs e) {
        //    Debug.WriteLine(callerName());
        //    HasError.Value = ErrorLevel.ERROR;
        //    CurrentError.Value = "unknown url scheme was specified.";
        //}

        //private void WebView_UnsafeContentWarningDisplaying(object sender, object e) {
        //    Debug.WriteLine(callerName());
        //    HasError.Value = ErrorLevel.ERROR;
        //    CurrentError.Value = "SmartScreen says, 'unsafe content'.";
        //}

        #endregion

        #region Interaction

        //private void WebView_ScriptNotify(object sender, WebViewControlScriptNotifyEventArgs e) {
        //    Debug.WriteLine($"{callerName()} {e.Value}");
        //    switch(e.Value[0]) {
        //        case 'i':
        //            StatusLine.Value = e.Value.Substring(2);
        //            break;
        //        case 'o':
        //            LMonitor.ResumeStatusLine();
        //            break;
        //        //case 'c':
        //        //    CopyCommand.Execute(e.Value.Substring(2));
        //        //    break;
        //        default:
        //            Debug.Assert(false, $"unknown command:{e.Value}");
        //            break;
        //    }
        //}

        #endregion


        #region Fatal Error

        private void WebView_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e) {
            Debug.WriteLine(callerName());
            HasError.Value = ErrorLevel.FATAL_ERROR;
            CurrentError.Value = "WebView has been dead. Please reboot DxxBrowser.";
        }

        #endregion

        #region Unknown Events

        //private void WebView_MoveFocusRequested(object sender, WebViewControlMoveFocusRequestedEventArgs e) {
        //    Debug.WriteLine(callerName());
        //}

        private void WebView_SourceUpdated(object sender, DataTransferEventArgs e) {
            Debug.WriteLine(callerName());
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            Debug.WriteLine(callerName());
        }
        private void WebView_WebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e) {
            //Debug.WriteLine($"{callerName()} URL={e.Request.Uri}");
            LinkedResources.OnNext(e.Request.Uri);
        }

        private void WebView_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e) {
            Debug.WriteLine(callerName());
        }

        private void WebView_DocumentTitleChanged(object sender, object e) {
            Debug.WriteLine($"{callerName()} title={Browser?.CoreWebView2?.DocumentTitle}");
        }

        private void WebView_HistoryChanged(object sender, object e) {
            Debug.WriteLine(callerName());
            UpdateHistory();
        }

        private void WebView_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e) {
            Debug.WriteLine($"{callerName()} new={e.IsNewDocument}");
            if(!e.IsNewDocument) {
                Url.Value = Browser.Source.ToString();
            }
        }

        #endregion
    }
#pragma warning restore CS0618 // 型またはメンバーが古い形式です
}
