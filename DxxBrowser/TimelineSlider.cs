using Reactive.Bindings;
using System;
using System.Reactive.Subjects;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace DxxBrowser {
    public interface ITimelineOwner {
        ReactiveProperty<bool> IsPlaying { get; }
        double SeekPosition { get; set; }
        void Play();
        void Pause();
    }

    public class TimelineSlider : Slider {
        public ReactiveProperty<bool> Dragging { get; } = new ReactiveProperty<bool>();

        private WeakReference<ITimelineOwner> mOwner;
        private ReadOnlyReactiveProperty<bool> IsPlaying;

        public DispatcherTimer mTimer;

        public TimelineSlider() {
            mTimer = new DispatcherTimer();
            mTimer.Interval = TimeSpan.FromMilliseconds(50);
            mTimer.Tick += (s, e) => {
                var player = Owner;
                if (null != player) {
                    this.Value = player.SeekPosition;
                }
            };
        }

        public void Initialize(ITimelineOwner owner) {
            Owner = owner;
            IsPlaying = owner.IsPlaying.ToReadOnlyReactiveProperty();
            IsPlaying.Subscribe((v) => {
                if(v) {
                    mTimer.Start();
                } else {
                    mTimer.Stop();
                }
            });
        }

        public ITimelineOwner Owner {
            get => mOwner?.GetValue();
            set => mOwner = new WeakReference<ITimelineOwner>(value);
        }
        private bool mOrgPlaying = false;
        protected override void OnThumbDragStarted(DragStartedEventArgs e) {
            base.OnThumbDragStarted(e);
            mOrgPlaying = IsPlaying.Value;
            Owner?.Pause();
        }

        protected override void OnThumbDragDelta(DragDeltaEventArgs e) {
            base.OnThumbDragDelta(e);
            Owner.SeekPosition = this.Value;
        }

        protected override void OnThumbDragCompleted(DragCompletedEventArgs e) {
            base.OnThumbDragCompleted(e);
            if (mOrgPlaying) {
                Owner?.Play();
            }
        }
    }
}
