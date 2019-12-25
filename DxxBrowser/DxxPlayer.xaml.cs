using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using Reactive.Bindings;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayer.xaml の相互作用ロジック
    /// </summary>
    /// 
    public partial class DxxPlayer : Window {
        private DxxPlayer() {
            InitializeComponent();
        }

        private class SourceReserver : DxxViewModelBase, DxxPlayerView.IPlayList {
            public ReactiveProperty<ObservableCollection<string>> PlayList { get; } = new ReactiveProperty<ObservableCollection<string>>(new ObservableCollection<string>());
            public ReactiveProperty<int> CurrentIndex { get; } = new ReactiveProperty<int>(0);

            public SourceReserver() {
            }

            public bool Contains(string source) {
                return PlayList.Value.Contains(source);
            }

            public void AddSource(string source) {
                if(!Contains(source)) {
                    PlayList.Value.Add(source);
                }
            }
        }

        private static SourceReserver sReserver = new SourceReserver();
        private static DxxPlayer sPlayer = null;

        public static DxxPlayerView.IPlayList PlayList => sReserver;

        public static void ShowPlayer(Window ownerWindow) {
            if(sPlayer==null) {
                sPlayer = new DxxPlayer();
                if(ownerWindow!=null) {
                    sPlayer.Owner = ownerWindow;
                }
                sPlayer.Show();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            mPlayer.Initialize(PlayList);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            sPlayer = null;
        }

        public static void Terminate() {
            sPlayer?.Close();
            sReserver?.Dispose();
            sReserver = null;
        }

    }
}
