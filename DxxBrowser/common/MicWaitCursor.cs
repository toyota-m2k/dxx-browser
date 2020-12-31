using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DxxBrowser.common {
    public class MicWaitCursor : IDisposable {
        private WeakReference<FrameworkElement> CursorOwner;

        public Cursor Cursor {
            get => CursorOwner?.GetValue()?.Cursor;
            set {
                var co = CursorOwner?.GetValue();
                if(null!=co) {
                    co.Cursor = value;
                }
            }
        }

        private Cursor OrgCursor = null;

        public MicWaitCursor(FrameworkElement cursorOwner, Cursor waitCursor=null) {
            if (null != cursorOwner) {
                CursorOwner = new WeakReference<FrameworkElement>(cursorOwner);
                OrgCursor = cursorOwner.Cursor;
                cursorOwner.Cursor = waitCursor ?? Cursors.Wait;
            }
        }

        public void Dispose() {
            if (null != OrgCursor) {
                Cursor = OrgCursor;
                OrgCursor = null;
            }
        }

        public static MicWaitCursor Start(FrameworkElement cursorOwner, Cursor waitCursor=null) {
            return new MicWaitCursor(cursorOwner, waitCursor);
        }
    }
}
