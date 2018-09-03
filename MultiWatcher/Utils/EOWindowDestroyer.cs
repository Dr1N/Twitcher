using MultiWatcher.Utils;
using System;
using System.Text;

namespace OeBrowser.Utils
{
    static class EOWindowDestroyer
    {
        #region Fields

        private static string eoClass = "eo.nativewnd";
        private const UInt32 WM_CLOSE = 0x0010;

        #endregion

        #region Public  
        
        public static void CloseEOWindow()
        {
            IntPtr hWnd = GetEOWindow();
            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        #endregion

        #region Private

        private static IntPtr GetEOWindow()
        {
            IntPtr result = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if ((NativeMethods.IsWindowVisible(hWnd)))
                {
                    StringBuilder className = new StringBuilder();
                    NativeMethods.GetClassName(hWnd, className, className.Capacity);
                    if (className.ToString().StartsWith(eoClass))
                    {
                        result = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            return result;
        }

        #endregion
    }
}
