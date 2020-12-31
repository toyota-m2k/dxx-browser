using Microsoft.WindowsAPICodePack.Dialogs;
using DxxBrowser.driver;
using HtmlAgilityPack;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Common;

namespace DxxBrowser {
    public class DxxAnalysysViewModel : MicViewModelBase, IDisposable {
        #region Properties

        public ReactiveProperty<string> BaseUrl { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> XPath { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<bool> DocumentAvailable { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<IEnumerable<DxxHtmlNode>> Nodes { get; } = new ReactiveProperty<IEnumerable<DxxHtmlNode>>();
        public ReactiveProperty<DxxHtmlNode> CurrentNode { get; } = new ReactiveProperty<DxxHtmlNode>();
        public ReactiveProperty<DxxHtmlNode> SelectedNode { get; } = new ReactiveProperty<DxxHtmlNode>();
        public ReactiveCollection<string> XPathList { get; } = new ReactiveCollection<string>();

        private void InitializeProperties() {
            XPathList.Add(".//a[contains(@href,'.mp') or contains(@href,'.wmv') or contains(@href,'.mov')]");
            XPathList.Add(".//iframe");
            XPathList.Add(".//frame");
        }

        #endregion

        #region Commands

        public ReactiveCommand<string> BeginAnalysis { get; } = new ReactiveCommand<string>();

        public ReactiveCommand<string> ApplyXPath { get; } = new ReactiveCommand<string>();

        public ReactiveCommand<DxxLink> CopyLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> AnalizeLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> AnalizeNewLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> ExecuteLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> DownloadLinkUrl { get; } = new ReactiveCommand<DxxLink>();

        public ReactiveCommand<DxxHtmlNode> SelectParent { get; } = new ReactiveCommand<DxxHtmlNode>();
        public ReactiveCommand<DxxHtmlNode> SelectThisNode { get; } = new ReactiveCommand<DxxHtmlNode>();
        public ReactiveCommand<HtmlAttribute> CopyAttrValue { get; } = new ReactiveCommand<HtmlAttribute>();
        public ReactiveCommand<HtmlAttribute> CopyAttrName { get; } = new ReactiveCommand<HtmlAttribute>();

        //private string ensureName(string s) {
        //    if(s.EndsWith("/")) {
        //        s = s.Substring(0, s.Length - 1) + ".html";
        //    }
        //    return s.Replace("/", "_").Replace("\\", "_");
        //}

        public Uri EnsureUri(string url) {
            if(url.StartsWith("http")) {
                return new Uri(url);
            }
            var baseUri = new Uri(BaseUrl.Value);
            if(Uri.TryCreate(baseUri, url, out var r)) {
                return r;
            }
            return null;
        }

        private void InitializeCommands() {
            BeginAnalysis.Subscribe((v) => {
                BaseUrl.Value = EnsureUri(v)?.ToString();
                Load();
            });

            ApplyXPath.Subscribe((v) => {
                SetXPath(v);
            });

            CopyLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v.Value);
                var url = EnsureUri(v.Value)?.ToString();
                if (url != null) {
                    Clipboard.SetData(DataFormats.Text, url);
                }
            });
            ExecuteLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                var url = EnsureUri(v.Value)?.ToString();
                if(url!=null) {
                    try {
                        System.Diagnostics.Process.Start(url);
                    } catch (Exception ex) {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            });
            AnalizeLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                BeginAnalysis.Execute(v.Value);
            });
            AnalizeNewLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                var url = EnsureUri(v.Value)?.ToString();
                if (url != null) {
                    var aw = new DxxAnalysisWindow(url);
                    aw.Show();
                }
            });
            DownloadLinkUrl.Subscribe((v) => {
                using (var dlg = new CommonSaveFileDialog("Download to file.")) {
                    var uri = EnsureUri(v.Value);
                    if (uri != null) {
                        var ti = new DxxTargetInfo(uri, DxxUrl.GetFileName(uri), "");
                        dlg.OverwritePrompt = true;
                        dlg.DefaultFileName = DxxUrl.TrimName(ti.Name);
                        dlg.RestoreDirectory = true;
                        if (dlg.ShowDialog(Owner) == CommonFileDialogResult.Ok) {
                            DxxDownloader.Instance.Reserve(ti, dlg.FileName, 0, (f) => {
                                Owner.Dispatcher.InvokeAsync(() => {
                                    if (f== DxxDownloadingItem.DownloadStatus.Completed) {
                                        DxxFileDispositionDialog.Show(dlg.FileName, Owner);
                                    }
                                });
                            });
                        }
                    }
                }
            });

            SelectParent.Subscribe((v) => {
                var parent = v.Node.ParentNode;
                if(null!=parent) {
                    ApplyXPath.Execute(parent.XPath);
                }
            });

            SelectThisNode.Subscribe((v) => {
                //Debug.WriteLine(v);
                ApplyXPath.Execute(v.Node.XPath);
            });

            CopyAttrName.Subscribe((v) => {
                Clipboard.SetData(DataFormats.Text, v.Name);
            });
            CopyAttrValue.Subscribe((v) => {
                Clipboard.SetData(DataFormats.Text, v.Value??"");
            });
        }

        #endregion

        #region Initialize
        private WeakReference<DxxAnalysisWindow> mOwner;
        private DxxAnalysisWindow Owner => mOwner?.GetValue();

        public DxxAnalysysViewModel(DxxAnalysisWindow owner, string initialUrl) {
            mOwner = new WeakReference<DxxAnalysisWindow>(owner);
            if (!string.IsNullOrEmpty(initialUrl)) {
                BaseUrl.Value = initialUrl;
                Load();
            }

            InitializeCommands();
            InitializeProperties();
        }

        #endregion

        #region Analyzing

        HtmlWeb Web = new HtmlWeb();
        HtmlDocument HtmlRoot = null;

        public static string LOG_CAT = "ANALYZER";

        private async void Load() {
            if(string.IsNullOrWhiteSpace(BaseUrl.Value)) {
                return;
            }
            DocumentAvailable.Value = false;
            try {
                Nodes.Value = null;
                CurrentNode.Value = null;
                //HtmlRoot = await Web.LoadFromWebAsync(BaseUrl.Value);
                HtmlRoot = Web.LoadFromBrowser(BaseUrl.Value);
                if (null!=HtmlRoot) {
                    DocumentAvailable.Value = true;
                    CurrentNode.Value = new DxxHtmlNode(HtmlRoot.DocumentNode);
                    Nodes.Value = CurrentNode.Value.ChildNodes;
                }
            } catch(Exception e) {
                Debug.WriteLine(e.ToString());
                DxxLogger.Instance.Error(LOG_CAT, e.Message);
            }
        }

        public void SetXPath(string xpath) {
            if(string.IsNullOrEmpty(xpath)) {
                XPath.Value = "";
                CurrentNode.Value = new DxxHtmlNode(HtmlRoot.DocumentNode);
                Nodes.Value = CurrentNode.Value.ChildNodes;
                return;
            }
            var v = CurrentNode?.Value.SelectNodes(xpath);
            if(Utils.IsNullOrEmpty(v)) {
                return;
            }
            XPath.Value = xpath;
            Nodes.Value = v;
        }

        #endregion
    }

    /// <summary>
    /// DxxAnalysisWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxAnalysisWindow : Window {
        public DxxAnalysisWindow(string url) {
            ViewModel = new DxxAnalysysViewModel(this, url);
            InitializeComponent();
            AnalysisWindows = AnalysisWindows.Concat(new WeakReference<DxxAnalysisWindow>[] {new WeakReference<DxxAnalysisWindow>(this)});
        }

        private static IEnumerable<WeakReference<DxxAnalysisWindow>> AnalysisWindows = new WeakReference<DxxAnalysisWindow>[0];
        public static void Terminate() {
            var list = AnalysisWindows;
            foreach (var v in list) {
                var win = v.GetValue();
                win?.Close();
            }
        }



        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
            AnalysisWindows = AnalysisWindows.Where((v) => {
                var win = v.GetValue();
                return null != win && win != this;
            });
        }

        public DxxAnalysysViewModel ViewModel {
            get => DataContext as DxxAnalysysViewModel;
            private set { DataContext = value; }
        }

        private void OnNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            ViewModel.SelectedNode.Value = e.NewValue as DxxHtmlNode;
        }

        private void OnLinkDoubleClick(object sender, MouseButtonEventArgs e) {
            var v = (sender as ListViewItem)?.Content as DxxLink;

            if(v !=null) {
                ViewModel.ExecuteLinkUrl.Execute(v);
            }
        }

    }
}
