using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Point = System.Drawing.Point;

namespace Common {
    public class WinPlacement
    {
        #region Win32データ型
        private static bool pre_equals(object me, object p)
        {
            // If parameter is null, return false.
            if (Object.ReferenceEquals(p, null))
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(me, p))
            {
                return true;
            }
            // If run-time types are not exactly the same, return false.
            if (me.GetType() != p.GetType())
            {
                return false;
            }
            return true;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }

            public override bool Equals(object p)
            {
                if (!pre_equals(this, p))
                {
                    return false;
                }
                RECT s = (RECT)p;
                return s.Left == Left && s.Right == Right && s.Top == Top && s.Bottom == Bottom;
            }

            //public bool Equals(RECT s)
            //{
            //    return s.Left == Left && s.Right == Right && s.Top == Top && s.Bottom == Bottom;
            //}


            public static bool operator ==(RECT r0, RECT r1)
            {
                return r0.Equals(r1);
            }
            public static bool operator !=(RECT r0, RECT r1)
            {
                return !r0.Equals(r1);
            }

            public override int GetHashCode()
            {
                // どうせ使わないから
                return base.GetHashCode();
            }
        }

        // POINT structure required by WINDOWPLACEMENT structure
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public override bool Equals(object obj)
            {
                if (!pre_equals(this, obj))
                {
                    return false;
                }
                POINT s = (POINT)obj;
                return s.X == X && s.Y == Y;
            }

            public static bool operator ==(POINT r0, POINT r1)
            {
                return r0.Equals(r1);
            }
            public static bool operator !=(POINT r0, POINT r1)
            {
                return !r0.Equals(r1);
            }

            public override int GetHashCode()
            {
                // どうせ使わないから
                return base.GetHashCode();
            }

        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;

            public override bool Equals(object obj)
            {
                if (!pre_equals(this, obj))
                {
                    return false;
                }
                WINDOWPLACEMENT s = (WINDOWPLACEMENT)obj;
                return s.flags == flags &&
                        s.showCmd == showCmd &&
                        s.minPosition == minPosition &&
                        s.maxPosition == maxPosition &&
                        s.normalPosition == normalPosition;
            }

            public static bool operator ==(WINDOWPLACEMENT r0, WINDOWPLACEMENT r1)
            {
                return r0.Equals(r1);
            }
            public static bool operator !=(WINDOWPLACEMENT r0, WINDOWPLACEMENT r1)
            {
                return !r0.Equals(r1);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        #endregion

        #region Win32 API declarations to set and get window placement
        [DllImport("user32.dll")]
        static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        const int SW_SHOWNORMAL = 1;
        const int SW_SHOWMINIMIZED = 2;
        #endregion


        private static WINDOWPLACEMENT GetWindowPlacement(Window w)
        {
            WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            GetWindowPlacement(hwnd, out wp);
            return wp;
        }

        private static void SetWindowPlacement(Window w, WINDOWPLACEMENT wp)
        {
            wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            wp.flags = 0;
            wp.showCmd = (wp.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : wp.showCmd);
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            SetWindowPlacement(hwnd, ref wp);
        }


        public bool HasValue
        {
            get; set;
        }

        public WINDOWPLACEMENT Placement
        {
            get; set;
        }


        public WinPlacement()
        {
            Placement = new WINDOWPLACEMENT();
            HasValue = false;
        }

        public void GetPlacementFrom(Window w)
        {
            var wp = GetWindowPlacement(w);
            if (Placement != wp)
            {
                HasValue = true;
                Placement = wp;
            }
        }

        public void ApplyPlacementTo(Window w)
        {
            if (HasValue)
            {
                SetWindowPlacement(w, Placement);
            }
        }



        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
        const uint GA_PARENT = 1;
        const uint GA_ROOT = 2;
        const uint GA_ROOTOWNER = 3;

        [DllImport("user32.dll")]
        public static extern int GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_SHOWWINDOW = 0x0040;

        //[DllImport("user32.dll")]
        //public static extern bool GetCursorPos(ref POINT lpPoint);

        public static Window GetWindowFromPoint(Point point)
        {
            var hwnd = WindowFromPoint(new POINT(point.X, point.Y));
            if (hwnd == IntPtr.Zero) return null;
            var p = GetParent(hwnd);
            while (p != IntPtr.Zero)
            {
                hwnd = p;
                p = GetParent(hwnd);
            }
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsVisible)
                {
                    var helper = new WindowInteropHelper(w);
                    if (helper.Handle == hwnd) return w;
                }
            }
            return null;
        }

        public static Rectangle? GetWindowPositionAtPoint(Point pos)
        {
            var hwnd = WindowFromPoint(new POINT(pos.X, pos.Y));
            if (hwnd == IntPtr.Zero) return null;
            var p = GetAncestor(hwnd, GA_ROOTOWNER);
            if (p != IntPtr.Zero)
            {
                RECT rc = new RECT();
                GetWindowRect(p, out rc);
                return new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
            }
            return null;

        }

        public static bool SetWindowPositionAtPoint(Point pos, Rectangle rc)
        {
            uint flags = SWP_NOZORDER;
            if (rc.Width == 0 && rc.Height == 0)
            {
                flags |= SWP_NOSIZE;
            }
            var hwnd = WindowFromPoint(new POINT(pos.X, pos.Y));
            if (hwnd == IntPtr.Zero) return false;
            var p = GetAncestor(hwnd, GA_ROOTOWNER);
            if (p != IntPtr.Zero)
            {
                SetWindowPos(p, IntPtr.Zero, rc.Left, rc.Top, rc.Width, rc.Height, flags);
                return true;
            }
            return false;
        }
    }
}
