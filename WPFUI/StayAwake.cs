using System;
using System.Runtime.InteropServices;

namespace WPFUI
{
    internal static class StayAwake
    {
        private const uint Continuous = 0x80000000;
        private const uint SystemRequired = 0x00000001;
        private const int PowerRequestSystemRequired = 1;
        private const int PowerRequestExecutionRequired = 3;

        private static readonly object Gate = new();
        private static IntPtr _request;
        private static IntPtr _reason;

        public static bool Hold()
        {
            lock (Gate)
            {
                if (_request != IntPtr.Zero) return true;

                _reason = Marshal.StringToHGlobalUni("Travian Guard está en ejecución");
                var context = new ReasonContext
                {
                    Version = 0,
                    Flags = 1,
                    SimpleReasonString = _reason,
                };
                _request = PowerCreateRequest(ref context);
                if (_request == IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_reason);
                    _reason = IntPtr.Zero;
                    return SetThreadExecutionState(Continuous | SystemRequired) != 0;
                }

                var system = PowerSetRequest(_request, PowerRequestSystemRequired);
                var execution = PowerSetRequest(_request, PowerRequestExecutionRequired);
                SetThreadExecutionState(Continuous | SystemRequired);
                if (system || execution) return true;

                ReleaseLocked();
                return false;
            }
        }

        public static void Release()
        {
            lock (Gate) ReleaseLocked();
        }

        private static void ReleaseLocked()
        {
            if (_request != IntPtr.Zero)
            {
                PowerClearRequest(_request, PowerRequestSystemRequired);
                PowerClearRequest(_request, PowerRequestExecutionRequired);
                CloseHandle(_request);
                _request = IntPtr.Zero;
            }

            if (_reason != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_reason);
                _reason = IntPtr.Zero;
            }

            SetThreadExecutionState(Continuous);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ReasonContext
        {
            public uint Version;
            public uint Flags;
            public IntPtr SimpleReasonString;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerSetRequest(IntPtr powerRequest, int requestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerClearRequest(IntPtr powerRequest, int requestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);
    }
}
