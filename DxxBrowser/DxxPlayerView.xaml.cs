using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
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
using System.Windows.Threading;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayerView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxPlayerView : UserControl {

        public class DxxPlayerViewModel : DxxViewModelBase, ITimelineOwnerPlayer {
            public ReactiveProperty<bool> IsPlaying { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> IsReady { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<double> Duration { get; } = new ReactiveProperty<double>(100);
            public ReactiveProperty<double> LargePositionChange { get; } = new ReactiveProperty<double>(10);
            public ReactiveProperty<double> SmallPositionChange { get; } = new ReactiveProperty<double>(1);

            public Subject<bool> Ended { get; } = new Subject<bool>();

            public ReactiveCommand PlayCommand { get; } = new ReactiveCommand();
            public ReactiveCommand PauseCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoBackCommand { get; } = new ReactiveCommand();
            public ReactiveCommand GoForwardCommand { get; } = new ReactiveCommand();


            public IObservable<bool> IsPlayingProperty => IsPlaying;
            private bool ToBePlayed = false;
            private WeakReference<MediaElement> mPlayer = null;

            public ReactiveCollection<Uri> PlayList { get; } = new ReactiveCollection<Uri>();
            private ReactiveProperty<int> CurrentIndex = new ReactiveProperty<int>(-1);

            public void SetSource(Uri source) {
                PlayList.Clear();
                Source = source;
            }
            public void AddSource(Uri source) {
                PlayList.Add(source);
            }
            public void AddSource(IEnumerable<Uri> sources) {
                PlayList.AddRangeOnScheduler(sources);
            }


            public MediaElement Player {
                get => mPlayer?.GetValue();
                private set => mPlayer = new WeakReference<MediaElement>(value);
            }

            public Uri Source {
                get => Player?.Source;
                set {
                    IsReady.Value = false;
                    Player?.Apply((v) => v.Source = value);
                }
            }

            public double SeekPosition {
                get => Player?.Run((v) => v.Position.TotalMilliseconds) ?? 0;
                set => Player?.Apply((v)=>v.Position = TimeSpan.FromMilliseconds(value));
            }

            public DxxPlayerViewModel() {
            }

            public void Initialize(MediaElement player) {
                Player = player;
                IsReady.Subscribe((v) => {
                    if(v && ToBePlayed) {
                        Play();
                    }
                });
                PlayList.CollectionChanged += OnPlayListChanged;
                CurrentIndex.Subscribe((v) => {
                    if(v>=PlayList.Count) {
                        Stop();
                    } else if(v<0) {
                        Stop();
                    } else {
                        Source = PlayList[v];
                        Play();
                    }
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
            }

            private void OnPlayListChanged(object sender, NotifyCollectionChangedEventArgs e) {
                if(PlayList.Count>0) {
                    if(CurrentIndex.Value<0) {
                        CurrentIndex.Value = 0;
                    } else if(CurrentIndex.Value < PlayList.Count) {
                        if (!ToBePlayed) {
                            Source = PlayList[CurrentIndex.Value];
                            Play();
                        }
                    } else { // CurrentIndex >= Count
                    }
                    HasNext.Value = CurrentIndex.Value < PlayList.Count-1;
                    HasPrev.Value = 0<CurrentIndex.Value;
                } else {
                    HasNext.Value = false;
                    HasPrev.Value = false;
                    CurrentIndex.Value = -1;
                }
            }

            public void Next() {
                if(CurrentIndex.Value<PlayList.Count-1) {
                    CurrentIndex.Value++;
                }
            }
            public void Prev() {
                if(0<CurrentIndex.Value) {
                    var v = CurrentIndex.Value - 1;
                    if(v>=PlayList.Count) {
                        v = PlayList.Count - 1;
                    }
                    CurrentIndex.Value = v;
                }
            }

            public void Play() {
                ToBePlayed = true;
                if (IsReady.Value) {
                    IsPlaying.Value = true;
                    Player?.Play();
                }
            }

            public void Pause() {
                ToBePlayed = false;
                if (IsPlaying.Value) {
                    IsPlaying.Value = false;
                    Player?.Pause();
                }
            }

            public void Stop() {
                ToBePlayed = false;
                IsPlaying.Value = false;
                if (IsReady.Value) {
                    Player?.Stop();
                }
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

        public void AddSource(Uri source) {
            ViewModel.AddSource(source);
        }

        public void AddSource(IEnumerable<Uri> sources) {
            ViewModel.AddSource(sources);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ViewModel.Initialize(mMediaElement);
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
    }
}
