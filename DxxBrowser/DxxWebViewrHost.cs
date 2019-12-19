using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Microsoft.Toolkit.Wpf.UI.Controls;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DxxBrowser {
#pragma warning disable CS0618 // 型またはメンバーが古い形式です
    public class DxxWebViewHost : DxxViewModelBase {
        #region Properties

        public enum ErrorLevel {
            NONE,
            ERROR,
            FATAL_ERROR,
        }

        public bool IsMain { get; }

        [Disposal(false)]
        private WeakReference<WebView> mBrowser;
        public WebView Browser => mBrowser?.GetValue();
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

        public ReactiveProperty<IDxxDriver> Driver { get; } = new ReactiveProperty<IDxxDriver>(DxxDriverManager.DEFAULT);
        public ReactiveProperty<bool> IsTarget { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsDownloadable { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsContainerList { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<ErrorLevel> HasError{ get; } = new ReactiveProperty<ErrorLevel>(ErrorLevel.NONE);
        public ReactiveProperty<string> CurrentError { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<ObservableCollection<string>> FrameUrls { get; } = new ReactiveProperty<ObservableCollection<string>>(new ObservableCollection<string>());

        private void InitializeProperties() {
            Url.Subscribe((v) => {
                var driver = DxxDriverManager.Instance.FindDriver(v);
                if (driver != null) {
                    Driver.Value = driver;
                    Uri uri = new Uri(v);
                    var dxxUrl = new DxxUrl(uri, driver, driver.GetNameFromUri(uri, "main"), "");
                    IsTarget.Value = driver.LinkExtractor.IsTarget(dxxUrl);
                    IsContainer.Value = driver.LinkExtractor.IsContainer(dxxUrl);
                    IsContainerList.Value = driver.LinkExtractor.IsContainerList(dxxUrl);
                    IsDownloadable.Value = IsTarget.Value || IsContainer.Value;
                } else {
                    Driver.Value = DxxDriverManager.DEFAULT;
                    IsTarget.Value = false;
                    IsContainer.Value = false;
                    IsContainerList.Value = false;
                    IsDownloadable.Value = false;
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
        public ReactiveCommand<string> BookmarkCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand<string> NavigateCommand { get; } = new ReactiveCommand<string>();
        public ReactiveCommand ClearURLCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AnalyzeCommand { get; } = new ReactiveCommand();
        public ReactiveCommand DownloadCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ListingCommand { get; } = new ReactiveCommand();
        public ReactiveCommand SetupDriverCommand { get; } = new ReactiveCommand();

        private DxxUrl CreateDxxUrl() {
            var driver = Driver.Value;
            var uri = new Uri(Url.Value);
            return new DxxUrl(uri, driver, driver.GetNameFromUri(uri, "link"), IsMain ? "from main" : "from sub");
        }

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
                if (string.IsNullOrEmpty(v)) {
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
            AnalyzeCommand.Subscribe(() => {
                var aw = new DxxAnalysisWindow(Url.Value);
                aw.Owner = Owner;
                aw.Show();
            });
            DownloadCommand.Subscribe(async () => {
                if(IsTarget.Value || IsContainer.Value) {
                    await CreateDxxUrl().Download();
                }
            });
            ListingCommand.Subscribe(async () => {
                if (IsContainerList.Value) {
                    var dxxUrl = CreateDxxUrl();
                    var targets = await dxxUrl.TryGetTargetContainers();
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
        }
        #endregion

        #region Events
        public Subject<DxxUrl> MainViewBeginAutoDownload { get; } = new Subject<DxxUrl>();
        public Subject<string> RequestLoadInSubview { get; } = new Subject<string>();
        #endregion

        #region Construction / Disposition

        public DxxWebViewHost(WebView wv, bool isMain, Window owner, DxxBookmark bookmarks,
            ReactiveProperty<ObservableCollection<DxxTargetInfo>> targetList) {
            IsMain = isMain;
            Bookmarks = new ReactiveProperty<DxxBookmark>(bookmarks);
            TargetList = targetList;
            mOwner = new WeakReference<Window>(owner);
            mBrowser = new WeakReference<WebView>(wv);
            Browser.NavigationStarting += WebView_NavigationStarting;
            Browser.NavigationCompleted += WebView_NavigationCompleted;
            Browser.SourceUpdated += WebView_SourceUpdated;
            Browser.ContentLoading += WebView_ContentLoading;
            Browser.DOMContentLoaded += WebView_DOMContentLoaded;
            Browser.FrameContentLoading += WebView_FrameContentLoading;
            Browser.FrameNavigationCompleted += WebView_FrameNavigationCompleted;
            Browser.FrameNavigationStarting += WebView_FrameNavigationStarting;
            Browser.FrameDOMContentLoaded += WebView_FrameDOMContentLoaded;
            Browser.UnsafeContentWarningDisplaying += WebView_UnsafeContentWarningDisplaying;
            Browser.UnsupportedUriSchemeIdentified += WebView_UnsupportedUriSchemeIdentified;
            Browser.UnviewableContentIdentified += WebView_UnviewableContentIdentified;
            Browser.ScriptNotify += WebView_ScriptNotify;
            Browser.LongRunningScriptDetected += WebView_LongRunningScriptDetected;
            Browser.NewWindowRequested += WebView_NewWindowRequested;
            Browser.PermissionRequested += WebView_PermissionRequired;
            Browser.MoveFocusRequested += WebView_MoveFocusRequested;
            Browser.Process.ProcessExited += WebView_ProcessExited;

            InitializeProperties();
            InitializeCommands();
        }

        public override void Dispose() {
            if(null==Browser) {
                return;
            }
            Browser.NavigationStarting -= WebView_NavigationStarting;
            Browser.NavigationCompleted -= WebView_NavigationCompleted;
            Browser.SourceUpdated -= WebView_SourceUpdated;
            Browser.ContentLoading -= WebView_ContentLoading;
            Browser.DOMContentLoaded -= WebView_DOMContentLoaded;
            Browser.FrameContentLoading -= WebView_FrameContentLoading;
            Browser.FrameNavigationCompleted -= WebView_FrameNavigationCompleted;
            Browser.FrameNavigationStarting -= WebView_FrameNavigationStarting;
            Browser.FrameDOMContentLoaded -= WebView_FrameDOMContentLoaded;
            Browser.UnsafeContentWarningDisplaying -= WebView_UnsafeContentWarningDisplaying;
            Browser.UnsupportedUriSchemeIdentified -= WebView_UnsupportedUriSchemeIdentified;
            Browser.UnviewableContentIdentified -= WebView_UnviewableContentIdentified;
            Browser.ScriptNotify -= WebView_ScriptNotify;
            Browser.LongRunningScriptDetected -= WebView_LongRunningScriptDetected;
            Browser.NewWindowRequested -= WebView_NewWindowRequested;
            Browser.PermissionRequested -= WebView_PermissionRequired;
            Browser.MoveFocusRequested -= WebView_MoveFocusRequested;
            Browser.Process.ProcessExited -= WebView_ProcessExited;
            base.Dispose();
        }

        #endregion

        #region Navigation

        struct Pending {
            public enum Command {
                None,
                Load,
                GoBack,
                GoForward,
                Reload,
            }
            public Command Waiting;
            public string Url;
            public Pending(Command cmd, string url=null) {
                Waiting = Command.None;
                Url = null;
            }
        }
        Pending PendingCommand = new Pending(Pending.Command.None);

        void Navigate(string url) {
            var uri = DxxUrl.FixUpUrl(url);
            if(null==uri) {
                return;
            }

            if (Loading.Value) {
                PendingCommand.Waiting = Pending.Command.Load;
                PendingCommand.Url = url;
                Browser.Stop();
            } else {
                PendingCommand.Waiting = Pending.Command.None;
                Browser.Navigate(url);
            }
        }

        void Stop() {
            PendingCommand.Waiting = Pending.Command.None;
            Browser.Stop();
        }

        void Reload() {
            if (Loading.Value) {
                PendingCommand.Waiting = Pending.Command.Reload;
                Browser.Stop();
            } else {
                PendingCommand.Waiting = Pending.Command.None;
                Browser.Refresh();
            }
        }

        void GoBack() {
            if (Loading.Value) {
                PendingCommand.Waiting = Pending.Command.GoBack;
                Browser.Stop();
            } else {
                PendingCommand.Waiting = Pending.Command.None;
                Browser.GoBack();
            }
        }

        void GoForward() {
            if (Loading.Value) {
                PendingCommand.Waiting = Pending.Command.GoForward;
                Browser.Stop();
            } else {
                PendingCommand.Waiting = Pending.Command.None;
                Browser.GoForward();
            }
        }

        void UpdateHistory() {
            HasPrev.Value = Browser.CanGoBack;
            HasNext.Value = Browser.CanGoForward;
        }

        int mLoading = 0;
        void UpdateLoading(bool loading) {
            if(loading) {
                if (0==mLoading) {
                    Loading.Value = true;
                }
                mLoading++;
            } else {
                mLoading--;
                if(0==mLoading) {
                    Loading.Value = false;
                }
            }
        }

        #endregion

        #region Frame List

        void ClearFrameList() {
            FrameUrls.Value.Clear();
            HasFrameLink.Value = false;
        }

        void AddFrameList(string url) {
            FrameUrls.Value.Remove(url);
            FrameUrls.Value.Insert(0, url);
            HasFrameLink.Value = FrameUrls.Value.Count > 0;
        }

        #endregion

        #region Navigation Events

        private void WebView_NavigationStarting(object sender, WebViewControlNavigationStartingEventArgs e) {
            Debug.WriteLine(callerName());
            var driver = DxxDriverManager.Instance.FindDriver(e.Uri.ToString());
            if (driver != null) {
                var du = new DxxUrl(e.Uri, driver, driver.GetNameFromUri(e.Uri, "link"), "");
                if (du.IsContainer || du.IsTarget) {
                    if(IsMain) {
                        MainViewBeginAutoDownload.OnNext(du);
                    }
                    _ = du.Download();
                    e.Cancel = true;
                    return;
                }
            }
            UpdateHistory();
        }

        private void WebView_ContentLoading(object sender, WebViewControlContentLoadingEventArgs e) {
            Debug.WriteLine(callerName());
            Url.Value = e.Uri.ToString();
            UpdateLoading(true);
            if (HasError.Value == ErrorLevel.ERROR) {
                HasError.Value = ErrorLevel.NONE;
            }
            ClearFrameList();
            UpdateHistory();
        }

        private void WebView_DOMContentLoaded(object sender, WebViewControlDOMContentLoadedEventArgs e) {
            Debug.WriteLine(callerName());
            UpdateHistory();
        }

        private void WebView_NavigationCompleted(object sender, WebViewControlNavigationCompletedEventArgs e) {
            Debug.WriteLine(callerName());
            Browser.Dispatcher.InvokeAsync(() => {
                UpdateHistory();
                UpdateLoading(false);
                switch (PendingCommand.Waiting) {
                    case Pending.Command.Load:
                        Browser.Navigate(PendingCommand.Url);
                        break;
                    case Pending.Command.GoBack:
                        Browser.GoBack();
                        break;
                    case Pending.Command.GoForward:
                        Browser.GoForward();
                        break;
                    case Pending.Command.Reload:
                        Browser.Refresh();
                        break;
                    case Pending.Command.None:
                    default:
                        break;
                }
                PendingCommand.Waiting = Pending.Command.None;
            });
        }

        #endregion

        #region Frame Loading Events

        private void WebView_FrameNavigationStarting(object sender, WebViewControlNavigationStartingEventArgs e) {
            Debug.WriteLine(callerName());
            UpdateHistory();
            if (HasError.Value == ErrorLevel.ERROR) {
                HasError.Value = ErrorLevel.NONE;
            }
        }
        private void WebView_FrameContentLoading(object sender, WebViewControlContentLoadingEventArgs e) {
            Debug.WriteLine(callerName());
            UpdateLoading(true);
            AddFrameList(e.Uri.ToString());
            UpdateHistory();
        }
        private void WebView_FrameDOMContentLoaded(object sender, WebViewControlDOMContentLoadedEventArgs e) {
            Debug.WriteLine(callerName());
            UpdateHistory();
        }

        private void WebView_FrameNavigationCompleted(object sender, WebViewControlNavigationCompletedEventArgs e) {
            Debug.WriteLine(callerName());
            UpdateLoading(false);
            UpdateHistory();
        }
        #endregion

        #region Special Caces

        private void WebView_NewWindowRequested(object sender, WebViewControlNewWindowRequestedEventArgs e) {
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
        private void WebView_PermissionRequired(object sender, WebViewControlPermissionRequestedEventArgs e) {
            Debug.WriteLine(callerName());
            e.PermissionRequest.Deny();
        }

        #endregion

        #region Potential Probrems
        private void WebView_LongRunningScriptDetected(object sender, WebViewControlLongRunningScriptDetectedEventArgs e) {
            Debug.WriteLine(callerName());
            e.StopPageScriptExecution = true;
            HasError.Value = ErrorLevel.ERROR;
            CurrentError.Value = "Long running script was detected and stopped it.";
        }

        private void WebView_UnviewableContentIdentified(object sender, WebViewControlUnviewableContentIdentifiedEventArgs e) {
            Debug.WriteLine(callerName());
            HasError.Value = ErrorLevel.ERROR;
            CurrentError.Value = "content in unknown type was received.";
        }

        private void WebView_UnsupportedUriSchemeIdentified(object sender, WebViewControlUnsupportedUriSchemeIdentifiedEventArgs e) {
            Debug.WriteLine(callerName());
            HasError.Value = ErrorLevel.ERROR;
            CurrentError.Value = "unknown url scheme was specified.";
        }

        private void WebView_UnsafeContentWarningDisplaying(object sender, object e) {
            Debug.WriteLine(callerName());
            HasError.Value = ErrorLevel.ERROR;
            CurrentError.Value = "SmartScreen says, 'unsafe content'.";
        }

        #endregion

        #region Interaction

        private void WebView_ScriptNotify(object sender, WebViewControlScriptNotifyEventArgs e) {
            Debug.WriteLine(callerName());
        }

        #endregion


        #region Fatal Error
        private void WebView_ProcessExited(object sender, object e) {
            Debug.WriteLine(callerName());
            HasError.Value = ErrorLevel.FATAL_ERROR;
            CurrentError.Value = "WebView has been dead. Please reboot DxxBrowser.";
        }

        #endregion

        #region Unknown Events

        private void WebView_MoveFocusRequested(object sender, WebViewControlMoveFocusRequestedEventArgs e) {
            Debug.WriteLine(callerName());
        }

        private void WebView_SourceUpdated(object sender, DataTransferEventArgs e) {
            Debug.WriteLine(callerName());
        }

        #endregion
    }
#pragma warning restore CS0618 // 型またはメンバーが古い形式です
}
