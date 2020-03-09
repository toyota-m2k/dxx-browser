using Common;
using DxxBrowser.driver;
using Reactive.Bindings;
using System;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayerView.xaml の相互作用ロジック
    /// </summary>
    public interface IDxxPlayItem {
        string Url { get; }   // key
        string Path { get; }
        string Description { get; }
    }
    public interface IDxxPlayList {
        ReactiveProperty<IDxxPlayItem> Current { get; }
        ReactiveProperty<bool> HasNext { get; }
        ReactiveProperty<bool> HasPrev { get; }
        ReactiveProperty<int> CurrentPos { get; }
        ReactiveProperty<int> TotalCount { get; }
        bool Next();
        bool Prev();
        //void AddSource(IDxxPlayItem source);
        //void DeleteSource(IDxxPlayItem source);
    }

    public partial class DxxPlayerView : UserControl {

        public class DxxPlayerViewModel : MicViewModelBase, ITimelineOwnerPlayer {
            public double SeekPosition {
                get {
                    return Player?.Run((player) => {
                        var pos = player.Position.TotalMilliseconds;
                        PositionText.Value = FormatDuration(pos);
                        return pos;
                    }) ?? 0;
                }
                set {
                    Player?.Apply((player) => {
                        PositionText.Value = FormatDuration(value);
                        player.Position = TimeSpan.FromMilliseconds(value);
                    });
                }
            }

            public ReactiveProperty<bool> IsPlaying { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> IsReady { get; } = new ReactiveProperty<bool>(false);
            //public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
            //public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            private ReadOnlyReactiveProperty<IDxxPlayItem> CurrentItem { get; set; }
            public ReadOnlyReactiveProperty<bool> HasNext { get; private set; }
            public ReadOnlyReactiveProperty<bool> HasPrev { get; private set; }


            public ReactiveProperty<double> Duration { get; } = new ReactiveProperty<double>(100);
            public ReactiveProperty<bool> ShowPanel { get; } = new ReactiveProperty<bool>(true);
            public Subject<bool> Ended { get; } = new Subject<bool>();

            public ReactiveProperty<string> DurationText { get; } = new ReactiveProperty<string>("0");
            public ReactiveProperty<string> PositionText { get; } = new ReactiveProperty<string>("0");

            public ReactiveCommand PlayCommand { get; } = new ReactiveCommand();
            public ReactiveCommand PauseCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoBackCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoForwardCommand { get; } = new ReactiveCommand();
            public ReactiveCommand TrashCommand { get; } = new ReactiveCommand();
            public ReactiveCommand FitCommand { get; } = new ReactiveCommand();

            public IObservable<bool> IsPlayingProperty => IsPlaying;
            public IObservable<double> DurationProperty => Duration;

            [Disposal(false)]
            public IDxxPlayList PlayList { get; set;  } = null;

            private string FormatDuration(double duration) {
                var t = TimeSpan.FromMilliseconds(duration);
                return string.Format("{0}:{1:00}:{2:00}", t.Hours, t.Minutes, t.Seconds);
                //$"{t.Hours}:{t.Minutes}.{t.Seconds}";

            }

            public void SetSource(Uri source) {
                PlayList = null;
                Source = source;
            }

            private WeakReference<MediaElement> mPlayer = null;
            public MediaElement Player {
                get => mPlayer?.GetValue();
                private set => mPlayer = new WeakReference<MediaElement>(value);
            }

            public Uri Source {
                get => Player?.Source;
                set {
                    if (Disposed) {
                        return;
                    }
                    IsReady.Value = false;
                    Player?.Apply((v) => {
                        if (value != null) {
                            //Idle = false;
                            v.Source = value;
                            Play();
                        } else {
                            Stop();
                            v.Source = null;
                            //Idle = true;
                        }
                    });
                }
            }

            public DxxPlayerViewModel(MediaElement player, IDxxPlayList reserver) {
                Initialize(player, reserver);
            }

            public void Initialize(MediaElement player, IDxxPlayList reserver) {
                Player = player;
                Duration.Subscribe((v) => {
                    DurationText.Value = FormatDuration(v);
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
                TrashCommand.Subscribe(() => {
                    Stop();
                    //PlayList?.DeleteSource(PlayList.Current.Value);
                    DxxNGList.Instance.RegisterNG(PlayList.Current.Value.Url);
                });
                FitCommand.Subscribe(() => {
                    Player.Stretch = (Player.Stretch == Stretch.UniformToFill) ? Stretch.Uniform : Stretch.UniformToFill;
                });

                if (reserver != null) {
                    PlayList = reserver;
                    HasNext = PlayList.HasNext.ToReadOnlyReactiveProperty();
                    HasPrev = PlayList.HasPrev.ToReadOnlyReactiveProperty();
                    CurrentItem = PlayList.Current.ToReadOnlyReactiveProperty();
                    CurrentItem.Subscribe((v) => {
                        Start();
                    });
                }
            }

            string mCurrentUrl = "";
            public void Start() {
                if (Disposed) {
                    return;
                }
                var item = PlayList.Current.Value;
                if(item!=null && item.Url!=mCurrentUrl && !string.IsNullOrEmpty(item.Path)) {
                    mCurrentUrl = item.Url;
                    Source = new Uri(item.Path);
                } else {
                    Stop();
                }
            }

            public void Next() {
                if (Disposed) {
                    return;
                }
                PlayList.Next();
            }

            public void Prev() {
                if (Disposed) {
                    return;
                }
                PlayList.Prev();
            }

            public void Play() {
                if (Disposed) {
                    return;
                }
                //Idle = false;
                IsPlaying.Value = true;
                Player?.Play();
            }

            public void Pause() {
                if (Disposed) {
                    return;
                }
                if (IsPlaying.Value) {
                    IsPlaying.Value = false;
                    Player?.Pause();
                }
            }

            public void Stop() {
                if(Disposed) {
                    return;
                }
                //Idle = true;
                IsPlaying.Value = false;
                Player?.Stop();
            }

            public bool Disposed => Player==null;

            //private bool mDisposed = false;
            public override void Dispose() {
                Player = null;
                base.Dispose();
            }
        }

        private DxxPlayerViewModel ViewModel {
            get => DataContext as DxxPlayerViewModel;
            set => DataContext = value;
        }

        //public Subject<bool> ReachEnd => ViewModel.Ended;

        public DxxPlayerView() {
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
            ViewModel = new DxxPlayerViewModel(mMediaElement, pl);
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
