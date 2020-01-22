using Common;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace DxxBrowser.driver {
    // SourceReserver
    public class DxxDownloadPlayLit : MicViewModelBase, IDxxPlayList {
        //public ReactiveProperty<ObservableCollection<IDxxPlayItem>> PlayList { get; } = new ReactiveProperty<ObservableCollection<IDxxPlayItem>>(new ObservableCollection<IDxxPlayItem>());
        //public ReactiveProperty<int> CurrentIndex { get; } = new ReactiveProperty<int>(0);

        public DxxDownloadPlayLit(DispatcherObject source) {
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
            if (null == source) {
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
            HasNext.Value = 0 < Sources.Count && CurrentPos.Value < Sources.Count;
            HasPrev.Value = 0 < Sources.Count && 1 < CurrentPos.Value;
        }

        public void DeleteSource(IDxxPlayItem source) {
            Dispatcher.Invoke(() => {
                var index = Sources.FindIndex((v) => {
                    return v.Url == source.Url;
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
                    DxxNGList.Instance.RegisterNG(item.Url);
                    UpdateStatus();
                }
            });
        }

        public bool Next() {
            if (CurrentPos.Value < Sources.Count) {
                CurrentPos.Value++;
                Current.Value = Sources[CurrentPos.Value - 1];
                UpdateStatus();
                return true;
            }
            mLast = true;
            return false;
        }

        public bool Prev() {
            if (1 < CurrentPos.Value) {
                CurrentPos.Value--;
                Current.Value = Sources[CurrentPos.Value - 1];
                UpdateStatus();
                return true;
            }
            return false;
        }
    }
}
