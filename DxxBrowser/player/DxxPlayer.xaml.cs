using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Common;
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

        private class SourceReserver : MicViewModelBase, IDxxPlayList {
            //public ReactiveProperty<ObservableCollection<IDxxPlayItem>> PlayList { get; } = new ReactiveProperty<ObservableCollection<IDxxPlayItem>>(new ObservableCollection<IDxxPlayItem>());
            //public ReactiveProperty<int> CurrentIndex { get; } = new ReactiveProperty<int>(0);

            public SourceReserver(DispatcherObject source) {
                DispatcherSource = source;
            }

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
            //public int CurrentIndex = 0;
            public ReactiveProperty<int> CurrentPos { get; } = new ReactiveProperty<int>(0);
            public ReactiveProperty<int> TotalCount { get; } = new ReactiveProperty<int>(0);

            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);

            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            private bool mLast = true;

            private WeakReference<DispatcherObject> mDispatherSource;
            private DispatcherObject DispatcherSource {
                get => mDispatherSource?.GetValue();
                set => mDispatherSource = new WeakReference<DispatcherObject>(value);
            }
            private Dispatcher Dispatcher => DispatcherSource?.Dispatcher;


            public void AddSource(IDxxPlayItem source) {
                if(null==source) {
                    return;
                }
                Dispatcher.Invoke(() => {
                    Sources.Add(source);
                    TotalCount.Value = Sources.Count;
                    if (mLast) {
                        mLast = false;
                        Current.Value = source;
                        CurrentPos.Value = Sources.Count;
                    }
                    UpdateStatus();
                });
            }

            private void UpdateStatus() {
                HasNext.Value = 0<Sources.Count && CurrentPos.Value < Sources.Count;
                HasPrev.Value = 0 < Sources.Count && 1 < CurrentPos.Value;
            }

            public void DeleteSource(IDxxPlayItem source) {
                Dispatcher.Invoke(() => {
                    var index = Sources.FindIndex((v) => {
                        return v.SourceUrl == source.SourceUrl;
                    });
                    if (index >= 0) {
                        var ci = CurrentPos.Value - 1;
                        var item = Sources[index];
                        if (ci == index) {
                            if (!Next() && !Prev()) {
                                Current.Value = null;
                            }
                        }
                        Sources.RemoveAt(index);
                        TotalCount.Value = Sources.Count;
                        if (ci > index) {
                            CurrentPos.Value--;
                        }
                        File.Delete(item.FilePath);
                        DxxNGList.Instance.RegisterNG(item.SourceUrl);
                    }
                    UpdateStatus();
                });
            }

            public bool Next() {
                if(CurrentPos.Value<Sources.Count) {
                    CurrentPos.Value++;
                    Current.Value = Sources[CurrentPos.Value-1];
                    UpdateStatus();
                    return true;
                }
                mLast = true;
                return false;
            }

            public bool Prev() {
                if (1< CurrentPos.Value) {
                    CurrentPos.Value--;
                    Current.Value = Sources[CurrentPos.Value-1];
                    UpdateStatus();
                    return true;
                }
                return false;
            }
        }

        private static SourceReserver sReserver = null;
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

        private ReadOnlyReactiveProperty<IDxxPlayItem> Current;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            mPlayer.Initialize(PlayList);
            Current = PlayList.Current.ToReadOnlyReactiveProperty();
            Current.Subscribe((v) => {
                if (null != v) {
                    Title = v.Description;
                } else {
                    Title = "No Sources";
                }
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            sPlayer = null;
            Current?.Dispose();
            Current = null;
        }

        public static void Initialize(DispatcherObject source) {
            sReserver = new SourceReserver(source);
        }

        public static void Terminate() {
            sPlayer?.Close();
            sPlayer = null;
            sReserver?.Dispose();
            sReserver = null;
        }
    }
}
