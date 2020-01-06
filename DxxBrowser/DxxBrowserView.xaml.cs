﻿using Microsoft.Toolkit.Wpf.UI.Controls;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DxxBrowser {
    /// <summary>
    /// DxxBrowserView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxBrowserView : UserControl, DxxWebViewManager.IDxxWebViewContainer {

        public DxxWebViewHost ViewModel {
            get => DataContext as DxxWebViewHost;
            set => DataContext = value;
        }

        public DxxBrowserView() {
            InitializeComponent();
        }

        public void Initialize(bool isMain, DxxBookmark bookmarks,
            ReactiveProperty<ObservableCollection<DxxTargetInfo>> targetList) {
            var vm = new DxxWebViewHost(isMain, Window.GetWindow(this), bookmarks, targetList);
            naviBar.ViewModel = vm;
            this.ViewModel = vm;
            DxxWebViewManager.Instance.PrepareBrowser(this);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            naviBar.ViewModel.Dispose();
        }

        public void OnWebViewLoaded(WebView wv) {
            browserHostGrid.Children.Clear();
            browserHostGrid.Children.Add(wv);
            naviBar.ViewModel.SetBrowser(wv);
        }
    }
}