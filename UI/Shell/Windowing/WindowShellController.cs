using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace zavod.UI.Shell.Windowing;

internal sealed class WindowShellController : IDisposable
{
    private const int GwlStyle = -16;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const int SwRestore = 9;
    private const int SwMinimize = 6;
    private const int SwMaximize = 3;

    private readonly IntPtr _hwnd;
    private readonly SubclassProc _subclassProc;
    private readonly GCHandle _selfHandle;
    private readonly AppWindow _appWindow;
    private readonly InputNonClientPointerSource _nonClientPointerSource;
    private bool _disposed;

    public WindowShellController(Window window, int minimumWidth, int minimumHeight)
    {
        _hwnd = WindowNative.GetWindowHandle(window);
        _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        _nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        MinimumWidth = minimumWidth;
        MinimumHeight = minimumHeight;
        ApplyPresenterChrome();

        _subclassProc = WindowSubclassCallback;
        _selfHandle = GCHandle.Alloc(this);
        if (!SetWindowSubclass(_hwnd, _subclassProc, 1, GCHandle.ToIntPtr(_selfHandle)))
        {
            throw new InvalidOperationException("Failed to install the window shell subclass.");
        }
    }

    public int MinimumWidth { get; }

    public int MinimumHeight { get; }

    public bool IsMaximized
    {
        get
        {
            var placement = WINDOWPLACEMENT.Create();
            return GetWindowPlacement(_hwnd, ref placement) && placement.showCmd == SwMaximize;
        }
    }

    public void SetCaptionRegions(IReadOnlyList<RectInt32> rects)
    {
        _nonClientPointerSource.ClearRegionRects(NonClientRegionKind.Caption);
        _nonClientPointerSource.SetRegionRects(NonClientRegionKind.Caption, rects is RectInt32[] array ? array : rects.ToArray());
    }

    public void SetPassthroughRegions(IReadOnlyList<RectInt32> rects)
    {
        _nonClientPointerSource.ClearRegionRects(NonClientRegionKind.Passthrough);
        _nonClientPointerSource.SetRegionRects(NonClientRegionKind.Passthrough, rects is RectInt32[] array ? array : rects.ToArray());
    }

    public void Minimize()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
            return;
        }

        ShowWindow(_hwnd, SwMinimize);
    }

    public void ToggleMaximize()
    {
        ShowWindow(_hwnd, IsMaximized ? SwRestore : SwMaximize);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        RemoveWindowSubclass(_hwnd, _subclassProc, 1);
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ApplyPresenterChrome()
    {
        if (_appWindow.Presenter is not OverlappedPresenter overlappedPresenter)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        if (_appWindow.Presenter is not OverlappedPresenter presenter)
        {
            throw new InvalidOperationException("Presenter-only top edge proof requires an overlapped presenter.");
        }

        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
    }

    private static IntPtr WindowSubclassCallback(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        nuint subclassId,
        IntPtr refData)
    {
        if (refData != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(refData);
            if (handle.Target is WindowShellController controller && message == WmGetMinMaxInfo)
            {
                controller.ApplyMinimumTrackSize(lParam);
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private void ApplyMinimumTrackSize(IntPtr lParam)
    {
        var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        minMaxInfo.ptMinTrackSize.x = MinimumWidth;
        minMaxInfo.ptMinTrackSize.y = MinimumHeight;
        Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: false);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc subclassProc,
        nuint subclassId,
        IntPtr refData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc subclassProc,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        nuint subclassId,
        IntPtr refData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;

        public static WINDOWPLACEMENT Create()
        {
            return new WINDOWPLACEMENT
            {
                length = Marshal.SizeOf<WINDOWPLACEMENT>()
            };
        }
    }
}
