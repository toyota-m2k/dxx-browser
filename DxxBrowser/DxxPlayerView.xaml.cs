using DxxBrowser.driver;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayerView.xaml の相互作用ロジック
    /// </summary>
    public interface IDxxPlayItem {
        string SourceUrl { get; }   // key
        string FilePath { get; }
    }
    public interface IDxxPlayList {
        ReactiveProperty<IDxxPlayItem> Current { get; }
        ReactiveProperty<bool> HasNext { get; }
        ReactiveProperty<bool> HasPrev { get; }
        bool Next();
        bool Prev();
        void AddSource(IDxxPlayItem source);
        void DeleteSource(IDxxPlayItem source);
    }

    public partial class DxxPlayerView : UserControl {

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
            public ReactiveCommand TrashCommand { get; } = new ReactiveCommand();

            public IObservable<bool> IsPlayingProperty => IsPlaying;
            public IObservable<double> DurationProperty => Duration;

            private bool Idle = true;
            private WeakReference<MediaElement> mPlayer = null;

            //public ReactiveCollection<Uri> PlayList { get; } = new ReactiveCollection<Uri>();
            //private ReactiveProperty<int> CurrentIndex = new ReactiveProperty<int>(-1);

            public IDxxPlayList PlayList { get; set;  } = null;

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
                        if (value != null) {
                            Idle = false;
                            v.Source = value;
                            Play();
                        } else {
                            Stop();
                            v.Source = null;
                            Idle = true;
                        }
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

            public void Initialize(MediaElement player, IDxxPlayList reserver) {
                Player = player;

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
                TrashCommand.Subscribe(() => {
                    Stop();
                    //if(0<=CurrentIndex.Value && CurrentIndex.Value<PlayList.Value.Count) {
                    //    var item = PlayList.Value[CurrentIndex.Value];
                    //    string path = item.FilePath;
                    //    File.Delete(path);
                    //    DxxNGList.Instance.RegisterNG(item.SourceUrl);
                    //}
                });

                if (reserver != null) {
                    PlayList = reserver;
                    PlayList.HasNext.Subscribe((v) => {
                        HasNext.Value = v;
                    });
                    PlayList.HasPrev.Subscribe((v) => {
                        HasPrev.Value = v;
                    });
                    PlayList.Current.Subscribe((v) => {
                        Start();
                    });
                }
            }

            string mCurrentUrl = "";
            public void Start() {
                var item = PlayList.Current.Value;
                if(item!=null && item.SourceUrl!=mCurrentUrl) {
                    mCurrentUrl = item.SourceUrl;
                    Source = new Uri(item.FilePath);
                } else {
                    Stop();
                }
            }

            public void Next() {
                PlayList.Next();
            }

            public void Prev() {
                PlayList.Prev();
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

        public void Initialize(IDxxPlayList pl) {
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
