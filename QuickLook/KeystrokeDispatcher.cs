// Copyright © 2017 Paddy Xu
// 
// This file is part of QuickLook program.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using QuickLook.Common.Helpers;
using QuickLook.Helpers;

namespace QuickLook
{
    internal class KeystrokeDispatcher : IDisposable
    {
        private static KeystrokeDispatcher _instance;

        private static HashSet<Keys> _validKeys;

        private GlobalKeyboardHook _hook;

        private bool _isPreviewRequest;
        private bool _spaceIsDown;
        private long _spaceHoldTick;
        private long _lastInvalidKeyPressTick;

        private const long HOLD_TO_PREVIEW_DURATION =
            TimeSpan.TicksPerMillisecond * 750;

        private const long VALID_KEY_PRESS_DELAY =
            TimeSpan.TicksPerSecond * 1;

        protected KeystrokeDispatcher()
        {
            InstallKeyHook(KeyDownEventHandler, KeyUpEventHandler);

            _validKeys = new HashSet<Keys>(new[]
            {
                Keys.Up,
                Keys.Down,
                Keys.Left,
                Keys.Right,
                Keys.Enter,
                Keys.Space,
                Keys.Escape
            });
        }

        public void Dispose()
        {
            _hook?.Dispose();
            _hook = null;
        }

        private void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            CallViewWindowManagerInvokeRoutine(e, true);
        }

        private void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            CallViewWindowManagerInvokeRoutine(e, false);
        }

        private void CallViewWindowManagerInvokeRoutine(
            KeyEventArgs e,
            bool isKeyDown)
        {
            // ------------------------------------------------------------
            // Invalid keys
            //
            // Only an invalid KEY DOWN starts/resets the delay.
            //
            // This is important for Alt+Tab:
            //
            //   Alt Down
            //   Tab Down
            //   Explorer becomes foreground
            //   Tab Up
            //   Alt Up
            //
            // The Alt/Tab KEY UP events must not reset
            // _lastInvalidKeyPressTick after Explorer becomes active.
            // Otherwise the first Space press can be suppressed
            // for VALID_KEY_PRESS_DELAY.
            // ------------------------------------------------------------
            if (!_validKeys.Contains(e.KeyCode))
            {
                Debug.WriteLine(
                    $"Invalid keypress: key={e.KeyCode}, " +
                    $"down={isKeyDown}, " +
                    $"time={_lastInvalidKeyPressTick}");

                if (isKeyDown)
                {
                    _lastInvalidKeyPressTick = DateTime.Now.Ticks;
                }

                return;
            }

            // ------------------------------------------------------------
            // Skip valid keys when modifiers are used.
            //
            // For example, Ctrl+Space / Alt+Space should not be treated
            // as a normal Space preview request.
            // ------------------------------------------------------------
            if (isKeyDown && e.Modifiers != Keys.None)
                return;

            // ------------------------------------------------------------
            // Skip if a valid key is pressed too soon after an invalid
            // key-down.
            // ------------------------------------------------------------
            if (DateTime.Now.Ticks - _lastInvalidKeyPressTick <
                VALID_KEY_PRESS_DELAY)
            {
                return;
            }

            _lastInvalidKeyPressTick = 0L;

            // ------------------------------------------------------------
            // Space handling.
            //
            // Do not immediately mark Space as down here.
            // Only record the time when the first Space-down is received.
            // ------------------------------------------------------------
            if (isKeyDown && e.KeyCode == Keys.Space)
            {
                if (_spaceIsDown)
                    return;

                _spaceHoldTick = DateTime.Now.Ticks;
            }

            // ------------------------------------------------------------
            // Check whether the current valid key is a preview request.
            // ------------------------------------------------------------
            if (isKeyDown)
            {
                _isPreviewRequest =
                    NativeMethods.QuickLook.GetFocusedWindowType() !=
                    NativeMethods.QuickLook.FocusedWindowType.Invalid;

                _isPreviewRequest |=
                    WindowHelper.IsForegroundWindowBelongToSelf();
            }
            // When isKeyDown is false, retain the existing
            // _isPreviewRequest state.

            // ------------------------------------------------------------
            // Call InvokeRoutine only when:
            //
            // 1. A valid key was pressed in a valid window, or
            // 2. A key is released after being pressed in a valid window.
            //
            // Space is special:
            // it must be held for 750 ms before its release is handled.
            // ------------------------------------------------------------
            if (_isPreviewRequest)
            {
                if (isKeyDown ||
                    e.KeyCode != Keys.Space ||
                    DateTime.Now.Ticks - _spaceHoldTick >=
                    HOLD_TO_PREVIEW_DURATION)
                {
                    InvokeRoutine(e.KeyCode, isKeyDown);

                    // Mark Space as down only after the Space key-down
                    // has actually been accepted.
                    if (isKeyDown && e.KeyCode == Keys.Space)
                    {
                        _spaceIsDown = true;
                    }
                }
            }

            // ------------------------------------------------------------
            // Reset variables when a key is released.
            // ------------------------------------------------------------
            if (!isKeyDown)
            {
                _isPreviewRequest = false;

                _spaceIsDown =
                    e.KeyCode != Keys.Space && _spaceIsDown;
            }
        }

        private void InvokeRoutine(Keys key, bool isKeyDown)
        {
            Debug.WriteLine(
                $"InvokeRoutine: key={key},down={isKeyDown}");

            if (isKeyDown)
            {
                switch (key)
                {
                    case Keys.Enter:
                        PipeServerManager.SendMessage(
                            PipeMessages.RunAndClose);
                        break;

                    case Keys.Space:
                        PipeServerManager.SendMessage(
                            PipeMessages.Toggle);
                        break;
                }
            }
            else
            {
                switch (key)
                {
                    case Keys.Up:
                    case Keys.Down:
                    case Keys.Left:
                    case Keys.Right:
                        PipeServerManager.SendMessage(
                            PipeMessages.Switch);
                        break;

                    case Keys.Escape:
                        PipeServerManager.SendMessage(
                            PipeMessages.Close);
                        break;

                    case Keys.Space:
                        PipeServerManager.SendMessage(
                            PipeMessages.Toggle);
                        break;
                }
            }
        }

        private void InstallKeyHook(
            KeyEventHandler downHandler,
            KeyEventHandler upHandler)
        {
            _hook = GlobalKeyboardHook.GetInstance();

            _hook.KeyDown += downHandler;
            _hook.KeyUp += upHandler;
        }

        internal static KeystrokeDispatcher GetInstance()
        {
            return _instance ??
                   (_instance = new KeystrokeDispatcher());
        }
    }
}
