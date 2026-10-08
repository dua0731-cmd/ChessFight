using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Minimal XInput reader (Windows) so the lab supports up to four gamepads without
    /// changing the project's input settings.
    /// </summary>
    public static class XInputPad
    {
        public const ushort Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
        public const ushort LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
        public const int MaxPads = 4;

        public struct Pad
        {
            public bool connected;
            public ushort buttons;
            public Vector2 left;
            public Vector2 right;
            public float leftTrigger;
            public float rightTrigger;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeState
        {
            public uint dwPacketNumber;
            public NativeGamepad Gamepad;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern int GetState14(int index, out NativeState state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        static extern int GetState910(int index, out NativeState state);
#endif

        static int api; // 0 untried, 1 xinput1_4, 2 xinput9_1_0, -1 unavailable
        static readonly Pad[] current = new Pad[MaxPads];
        static readonly Pad[] previous = new Pad[MaxPads];
        static readonly int[] nextProbeFrame = new int[MaxPads];
        static int polledFrame = -1;

        public static void Poll()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            for (int i = 0; i < MaxPads; i++)
            {
                previous[i] = current[i];
                // Polling an empty slot is slow in XInput, so probe disconnected pads only once a second.
                if (!current[i].connected && Time.frameCount < nextProbeFrame[i]) continue;
                current[i] = Read(i);
                if (!current[i].connected) nextProbeFrame[i] = Time.frameCount + 60;
            }
        }

        public static Pad Get(int index)
        {
            Poll();
            return current[index];
        }

        public static bool Held(int index, ushort button)
        {
            Poll();
            return current[index].connected && (current[index].buttons & button) != 0;
        }

        public static bool Pressed(int index, ushort button)
        {
            Poll();
            return current[index].connected && (current[index].buttons & button) != 0 && (previous[index].buttons & button) == 0;
        }

        /// <summary>The right trigger went past halfway this frame (a trigger has no button bit).</summary>
        public static bool RightTriggerPressed(int index)
        {
            Poll();
            return current[index].connected && current[index].rightTrigger > 0.5f && previous[index].rightTrigger <= 0.5f;
        }

        static Pad Read(int index)
        {
            var pad = new Pad();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (api < 0) return pad;
            NativeState state = default;
            int result = -1;
            if (api == 0 || api == 1)
            {
                try
                {
                    result = GetState14(index, out state);
                    api = 1;
                }
                catch (Exception)
                {
                    api = 2;
                }
            }
            if (api == 2)
            {
                try
                {
                    result = GetState910(index, out state);
                }
                catch (Exception)
                {
                    api = -1;
                    return pad;
                }
            }
            if (result != 0) return pad;
            pad.connected = true;
            pad.buttons = state.Gamepad.wButtons;
            pad.left = Stick(state.Gamepad.sThumbLX, state.Gamepad.sThumbLY);
            pad.right = Stick(state.Gamepad.sThumbRX, state.Gamepad.sThumbRY);
            pad.leftTrigger = state.Gamepad.bLeftTrigger / 255f;
            pad.rightTrigger = state.Gamepad.bRightTrigger / 255f;
#endif
            return pad;
        }

        static Vector2 Stick(short x, short y)
        {
            var v = new Vector2(x / 32767f, y / 32767f);
            float m = v.magnitude;
            const float dead = 0.24f;
            if (m < dead) return Vector2.zero;
            return v / m * Mathf.Min(1f, (m - dead) / (1f - dead));
        }
    }
}
