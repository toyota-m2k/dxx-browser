using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayerView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxPlayerView : UserControl {
        public interface IPlayList {
            ReactiveProperty<ObservableCollection<string>> PlayList { get; }
            ReactiveProperty<int> CurrentIndex { get; }
            void AddSource(string source);
        }

        public class DxxPlayerViewModel : DxxViewModelBase, ITimelineOwnerPlayer {
            public ReactiveProperty<bool> IsPlaying { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> IsReady { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<double> Duration { get; } = new ReactiveProperty<double>(100);
            public ReactiveProperty<bool> ShowPanel { get; } = new ReactiveProperty<bool>(true);

            public Subject<bool> Ended { get; } = new Subject<bool>();

            public ReactiveCommand PlayCommand { get; } = new ReactiveCommand();
            public ReactiveCommand PauseCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoBackCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoForwardCommand { get; } = new ReactiveCommand();

            public IObservable<bool> IsPlayingProperty => IsPlaying;
            public IObservable<double> DurationProperty => Duration;

            private bool Idle = true;
            private WeakReference<MediaElement> mPlayer = null;

            //public ReactiveCollection<Uri> PlayList { get; } = new ReactiveCollection<Uri>();
            //private ReactiveProperty<int> CurrentIndex = new ReactiveProperty<int>(-1);

            [Disposal(false)]
            public ReactiveProperty<ObservableCollection<string>> PlayList { get; set;  } = null;
            [Disposal(false)]
            public ReactiveProperty<int> CurrentIndex { get; set; } = null;

            public void SetSource(Uri source) {
                PlayList = null;
                Source = source;
            }

            public MediaElement Player {
                get => mPlayer?.GetValue();
                private set => mPlayer = new WeakReference<MediaElement>(value);
            }

            public Uri Source {
                get => Player?.Source;
                set {
                    IsReady.Value = false;
                    Player?.Apply((v) => {
                        Idle = false;
                        v.Source = value;
                        Play();
                    });
                }
            }

            public double SeekPosition {
                get {
                    return Player?.Run((player) => {
                        return player.Position.TotalMilliseconds;
                    }) ?? 0;
                }
                set {
                    Player?.Apply((player) => {
                        player.Position = TimeSpan.FromMilliseconds(value);
                    });
                }
            }

            public DxxPlayerViewModel() {
            }

            public void Initialize(MediaElement player, IPlayList reserver) {
                Player = player;
                if (reserver != null) {
                    PlayList = reserver.PlayList;
                    CurrentIndex = reserver.CurrentIndex;
                }

                PlayList.Value.CollectionChanged += OnPlayListChanged;
                CurrentIndex.Subscribe((v) => {
                    if(v<0 || PlayList.Value.Count<=v) {
                        Stop();
                    } else {
                        Source = new Uri(PlayList.Value[v]);
                    }
                    HasNext.Value = v < PlayList.Value.Count - 1;
                    HasPrev.Value = 0 < v;
                });

                Ended.Subscribe((v) => {
                    Next();
                });

                PlayCommand.Subscribe(() => {
                    Play();
                });

                PauseCommand.Subscribe(() => {
                    Pause();
                });

                GoForwardCommand.Subscribe(() => {
                    Next();
                });
                GoBackCommand.Subscribe(() => {
                    Prev();
                });
            }

            private void OnPlayListChanged(object sender, NotifyCollectionChangedEventArgs e) {
                if(PlayList.Value.Count>0) {
                    if(CurrentIndex.Value<0) {
                        CurrentIndex.Value = 0;
                    } else if(CurrentIndex.Value < PlayList.Value.Count) {
                        if (Idle) {
                            Source = new Uri(PlayList.Value[CurrentIndex.Value]);
                            Play();
                        }
                    } else { // CurrentIndex >= Count
                    }
                    HasNext.Value = CurrentIndex.Value < PlayList.Value.Count-1;
                    HasPrev.Value = 0<CurrentIndex.Value;
                } else {
                    HasNext.Value = false;
                    HasPrev.Value = false;
                    CurrentIndex.Value = -1;
                }
            }

            public void Next() {
                if(CurrentIndex.Value<PlayList.Value.Count) {
                    CurrentIndex.Value++;
                }
            }
            public void Prev() {
                if(0<CurrentIndex.Value) {
                    var v = CurrentIndex.Value - 1;
                    if(v>=PlayList.Value.Count) {
                        v = PlayList.Value.Count - 1;
                    }
                    CurrentIndex.Value = v;
                }
            }

            public void Play() {
                Idle = false;
                IsPlaying.Value = true;
                Player?.Play();
            }

            public void Pause() {
                if (IsPlaying.Value) {
                    IsPlaying.Value = false;
                    Player?.Pause();
                }
            }

            public void Stop() {
                Idle = true;
                IsPlaying.Value = false;
                Player?.Stop();
            }
        }

        private DxxPlayerViewModel ViewModel {
            get => DataContext as DxxPlayerViewModel;
            set => DataContext = value;
        }

        //public Subject<bool> ReachEnd => ViewModel.Ended;

        public DxxPlayerView() {
            ViewModel = new DxxPlayerViewModel();
            InitializeComponent();
        }

        public void SetSource(Uri source) {
            ViewModel.SetSource(source);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            //ViewModel.Initialize(mMediaElement);
            //mTimelineSlider.Initialize(ViewModel);
        }

        public void Initialize(IPlayList pl) {
            ViewModel.Initialize(mMediaElement, pl);
            mTimelineSlider.Initialize(ViewModel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
        }


        //public Uri Source {
        //    get => mMediaElement.Source;
        //    set {
        //        ViewModel.IsReady.Value = false;
        //        mMediaElement.Source = value;
        //    }
        //}

        private void OnMediaOpened(object sender, RoutedEventArgs e) {
            ViewModel.IsReady.Value = true;
            ViewModel.Duration.Value = mMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e) {
            ViewModel.Stop();
            ViewModel.Ended.OnNext(true);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) {
            ViewModel.IsReady.Value = false;
            ViewModel.Stop();
            ViewModel.Ended.OnNext(false);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e) {
            ViewModel.ShowPanel.Value = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) {
            ViewModel.ShowPanel.Value = false;
        }
    }
}
