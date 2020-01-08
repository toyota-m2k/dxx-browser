using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Microsoft.Toolkit.Wpf.UI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DxxBrowser {
    public class DxxWebViewManager {
#pragma warning disable CS0618 // 型またはメンバーが古い形式です
        public interface IDxxWebViewContainer {
            void AttachWebView(WebView wv);
            WebView DetachWebView();
        }

        private Dictionary<WebView, IDxxWebViewContainer> Views = new Dictionary<WebView, IDxxWebViewContainer>();

        private int ViewCount = 0;
        private WebView PrimaryWebView;
        private List<IDxxWebViewContainer> WaitingSubContainers = new List<IDxxWebViewContainer>();

        private DxxWebViewManager() {

        }

        public static DxxWebViewManager Instance { get; } = new DxxWebViewManager();

        public void PrepareBrowser(IDxxWebViewContainer host) {
            if(ViewCount==0) {
                ViewCount = 1;
                var wv = CreatePrimaryBrowser(host);
                host.AttachWebView(wv);
            } else {
                if(PrimaryWebView!=null) {
                    var wv = CreateSecondaryBrowser(host);
                    host.AttachWebView(wv);
                } else {
                    WaitingSubContainers.Add(host);
                }
            }
        }

        private WebView CreatePrimaryBrowser(IDxxWebViewContainer host) {
            Debug.Assert(PrimaryWebView == null);
            var wv = new WebView();
            wv.BeginInit();
            wv.EndInit();
            wv.IsScriptNotifyAllowed = true;
            Views.Add(wv, host);
            wv.Loaded += OnWebViewLoaded;
            wv.Unloaded += OnWebViewUnloaded;
            return wv;
        }

        private WebView CreateSecondaryBrowser(IDxxWebViewContainer host) {
            Debug.Assert(PrimaryWebView != null);
            var wv = new WebView(PrimaryWebView.Process);
            wv.BeginInit();
            wv.EndInit();
            wv.IsScriptNotifyAllowed = true;
            Views.Add(wv, host);
            wv.Unloaded += OnWebViewUnloaded;
            return wv;
        }

        private void OnWebViewLoaded(object sender, RoutedEventArgs e) {
            var wv = sender as WebView;
            wv.Loaded -= OnWebViewLoaded;
            if (PrimaryWebView==null) {
                PrimaryWebView = wv;
                wv.Process.ProcessExited += OnWebViewProcessExited;
                foreach (var h in WaitingSubContainers) {
                    h.AttachWebView(CreateSecondaryBrowser(h));
                }
                WaitingSubContainers.Clear();
            }
        }

        private void OnWebViewUnloaded(object sender, RoutedEventArgs e) {
            var wv = sender as WebView;
            Views.Remove(wv);
            wv.Unloaded -= OnWebViewUnloaded;
            if(PrimaryWebView==wv) {
                wv.Process.ProcessExited -= OnWebViewProcessExited;
                PrimaryWebView = null;
            }
        }

        private void OnWebViewProcessExited(object sender, object e) {
            (sender as WebViewControlProcess).ProcessExited -= OnWebViewProcessExited;

            var oldViews = Views;
            PrimaryWebView = null;
            WaitingSubContainers.Clear();
            Views = new Dictionary<WebView, IDxxWebViewContainer>();
            ViewCount = 0;
            foreach (var nv in oldViews) {
                try {
                    nv.Value.DetachWebView()?.Close();
                } catch (Exception ex) {
                    Debug.WriteLine(ex.ToString());
                }
                PrepareBrowser(nv.Value);
            }
            oldViews.Clear();
        }
#pragma warning restore CS0618 // 型またはメンバーが古い形式です
    }
}
