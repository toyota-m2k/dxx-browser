using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using DxxBrowser.driver;
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

        private class SourceReserver : DxxViewModelBase, IDxxPlayList {
            //public ReactiveProperty<ObservableCollection<IDxxPlayItem>> PlayList { get; } = new ReactiveProperty<ObservableCollection<IDxxPlayItem>>(new ObservableCollection<IDxxPlayItem>());
            //public ReactiveProperty<int> CurrentIndex { get; } = new ReactiveProperty<int>(0);

            //public SourceReserver() {
            //}

            //private bool Contains(string sourceUrl) {
            //    return !Utils.IsNullOrEmpty(PlayList.Value.Where((v) => v.SourceUrl == sourceUrl));
            //}

            //public void AddSource(IDxxPlayItem source) {
            //    if(!Contains(source.SourceUrl)) {
            //        PlayList.Value.Add(source);
            //    }
            //}
            private List<IDxxPlayItem> Sources = new List<IDxxPlayItem>();

            public ReactiveProperty<IDxxPlayItem> Current { get; } = new ReactiveProperty<IDxxPlayItem>();
            public int CurrentIndex = 0;

            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);

            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);

            public void AddSource(IDxxPlayItem source) {
                Sources.Add(source);
                if(Current.Value==null) {
                    Current.Value = source;
                    CurrentIndex = Sources.Count - 1;
                }
            }

            public void DeleteSource(IDxxPlayItem source) {
                var index = Sources.FindIndex((v) => {
                    return v.SourceUrl == source.SourceUrl;
                });
                if(index>=0) {
                    var item = Sources[index];
                    if(CurrentIndex==index) {
                        if (!Next() && !Prev()) {
                            Current.Value = null;
                        }
                    }
                    Sources.RemoveAt(index);
                    if(CurrentIndex>index) {
                        CurrentIndex--;
                    }
                    File.Delete(item.FilePath);
                    DxxNGList.Instance.RegisterNG(item.SourceUrl);
                }
            }

            public bool Next() {
                if(CurrentIndex<Sources.Count-1) {
                    CurrentIndex++;
                    Current.Value = Sources[CurrentIndex];
                    return true;
                }
                return false;
            }

            public bool Prev() {
                if (0<CurrentIndex) { 
                    CurrentIndex--;
                    Current.Value = Sources[CurrentIndex];
                    return true;
                }
                return false;
            }
        }

        private static SourceReserver sReserver = new SourceReserver();
        private static DxxPlayer sPlayer = null;

        public static IDxxPlayList PlayList => sReserver;

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
