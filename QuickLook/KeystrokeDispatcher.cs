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
using System.IO;
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

        // ------------------------------------------------------------
        // Debug log
        //
        // Log file:
        // QuickLook.exe directory\UserData\QuickLook-debug.log
        // ------------------------------------------------------------

        private static readonly object _logLock = new object();

        private static readonly string _logDirectory =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "UserData");

        private static readonly string _logFile =
            Path.Combine(
                _logDirectory,
                "QuickLook-debug.log");

        protected KeystrokeDispatcher()
        {
            Log("========================================");
            Log("KeystrokeDispatcher CREATED");
            Log("========================================");

            Log("BaseDirectory: " +
                AppDomain.CurrentDomain.BaseDirectory);

            Log("LogFile: " +
                _logFile);

            InstallKeyHook(
                KeyDownEventHandler,
                KeyUpEventHandler);

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

            Log("Valid keys initialized.");
        }

        public void Dispose()
        {
            Log("KeystrokeDispatcher DISPOSE");

            _hook?.Dispose();
            _hook = null;

            Log("Keyboard hook disposed.");
        }

        // ------------------------------------------------------------
        // File logging
        // ------------------------------------------------------------

        private static void Log(string message)
        {
            try
            {
                lock (_logLock)
                {
                    Directory.CreateDirectory(_logDirectory);

                    string line =
                        DateTime.Now.ToString(
                            "yyyy-MM-dd HH:mm:ss.fff") +
                        " | " +
                        message;

                    File.AppendAllText(
                        _logFile,
                        line +
                        Environment.NewLine);
                }
            }
            catch
            {
                // Never allow logging failure to affect QuickLook.
            }
        }

        private static void LogSeparator(string title)
        {
            Log("");
            Log("========== " + title + " ==========");
        }

        // ------------------------------------------------------------
        // Keyboard events
        // ------------------------------------------------------------

        private void KeyDownEventHandler(
            object sender,
            KeyEventArgs e)
        {
            LogSeparator("KEY DOWN");

            DumpKeyState(e, true);

            CallViewWindowManagerInvokeRoutine(
                e,
                true);
        }

        private void KeyUpEventHandler(
            object sender,
            KeyEventArgs e)
        {
            LogSeparator("KEY UP");

            DumpKeyState(e, false);

            CallViewWindowManagerInvokeRoutine(
                e,
                false);
        }

        // ------------------------------------------------------------
        // State dump
        // ------------------------------------------------------------

        private void DumpKeyState(
            KeyEventArgs e,
            bool isKeyDown)
        {
            Log("Key                = " +
                e.KeyCode);

            Log("KeyValue           = " +
                e.KeyValue);

            Log("Modifiers          = " +
                e.Modifiers);

            Log("isKeyDown          = " +
                isKeyDown);

            Log("_isPreviewRequest  = " +
                _isPreviewRequest);

            Log("_spaceIsDown       = " +
                _spaceIsDown);

            Log("_spaceHoldTick     = " +
                _spaceHoldTick);

            Log("_lastInvalidTick   = " +
                _lastInvalidKeyPressTick);

            try
            {
                var focusedWindowType =
                    NativeMethods.QuickLook.GetFocusedWindowType();

                Log("FocusedWindowType  = " +
                    focusedWindowType);
            }
            catch (Exception ex)
            {
                Log("FocusedWindowType  = ERROR: " +
                    ex.GetType().Name +
                    ": " +
                    ex.Message);
            }

            try
            {
                bool foregroundBelongToSelf =
                    WindowHelper.IsForegroundWindowBelongToSelf();

                Log("ForegroundIsSelf   = " +
                    foregroundBelongToSelf);
            }
            catch (Exception ex)
            {
                Log("ForegroundIsSelf   = ERROR: " +
                    ex.GetType().Name +
                    ": " +
                    ex.Message);
            }
        }

        // ------------------------------------------------------------
        // Main keyboard processing
        // ------------------------------------------------------------

        private void CallViewWindowManagerInvokeRoutine(
            KeyEventArgs e,
            bool isKeyDown)
        {
            LogSeparator(
                "CallViewWindowManagerInvokeRoutine");

            Log("Key=" +
                e.KeyCode +
                ", Down=" +
                isKeyDown);

            Log("State BEFORE processing:");
            Log("_isPreviewRequest = " +
                _isPreviewRequest);

            Log("_spaceIsDown = " +
                _spaceIsDown);

            Log("_spaceHoldTick = " +
                _spaceHoldTick);

            Log("_lastInvalidKeyPressTick = " +
                _lastInvalidKeyPressTick);

            // --------------------------------------------------------
            // Invalid keys
            // --------------------------------------------------------

            if (!_validKeys.Contains(e.KeyCode))
            {
                Log("RESULT: INVALID KEY");

                Log(
                    "Invalid key: " +
                    e.KeyCode +
                    ", Down=" +
                    isKeyDown);

                Log(
                    "Old _lastInvalidKeyPressTick = " +
                    _lastInvalidKeyPressTick);

                // IMPORTANT:
                // Keep the original 3.7.3 behavior here.
                //
                // We are diagnosing the original behavior and do not
                // want the diagnostic build to change the bug.
                _lastInvalidKeyPressTick =
                    DateTime.Now.Ticks;

                Log(
                    "New _lastInvalidKeyPressTick = " +
                    _lastInvalidKeyPressTick);

                Log(
                    "RETURN: invalid key");

                return;
            }

            Log("RESULT: VALID KEY");

            // --------------------------------------------------------
            // Modifiers
            // --------------------------------------------------------

            if (isKeyDown &&
                e.Modifiers != Keys.None)
            {
                Log(
                    "RETURN: valid key has modifier");

                Log(
                    "Modifiers = " +
                    e.Modifiers);

                return;
            }

            // --------------------------------------------------------
            // Invalid-key delay
            // --------------------------------------------------------

            long now =
                DateTime.Now.Ticks;

            long elapsed =
                now -
                _lastInvalidKeyPressTick;

            Log(
                "Invalid-key elapsed ticks = " +
                elapsed);

            Log(
                "VALID_KEY_PRESS_DELAY = " +
                VALID_KEY_PRESS_DELAY);

            if (elapsed <
                VALID_KEY_PRESS_DELAY)
            {
                Log(
                    "RETURN: valid key blocked by invalid-key delay");

                return;
            }

            Log(
                "Invalid-key delay passed.");

            _lastInvalidKeyPressTick = 0L;

            // --------------------------------------------------------
            // Space
            // --------------------------------------------------------

            if (isKeyDown &&
                e.KeyCode == Keys.Space)
            {
                Log(
                    "SPACE DOWN detected.");

                Log(
                    "_spaceIsDown = " +
                    _spaceIsDown);

                if (_spaceIsDown)
                {
                    Log(
                        "RETURN: Space is already marked as down.");

                    return;
                }

                _spaceHoldTick =
                    DateTime.Now.Ticks;

                Log(
                    "_spaceHoldTick = " +
                    _spaceHoldTick);
            }

            // --------------------------------------------------------
            // Preview request
            // --------------------------------------------------------

            if (isKeyDown)
            {
                try
                {
                    var focusedWindowType =
                        NativeMethods.QuickLook
                            .GetFocusedWindowType();

                    bool foregroundBelongToSelf =
                        WindowHelper
                            .IsForegroundWindowBelongToSelf();

                    Log(
                        "GetFocusedWindowType = " +
                        focusedWindowType);

                    Log(
                        "IsForegroundWindowBelongToSelf = " +
                        foregroundBelongToSelf);

                    _isPreviewRequest =
                        focusedWindowType !=
                        NativeMethods.QuickLook
                            .FocusedWindowType.Invalid;

                    _isPreviewRequest |=
                        foregroundBelongToSelf;

                    Log(
                        "_isPreviewRequest = " +
                        _isPreviewRequest);
                }
                catch (Exception ex)
                {
                    Log(
                        "ERROR while checking preview request: " +
                        ex.GetType().Name +
                        ": " +
                        ex.Message);

                    throw;
                }
            }
            else
            {
                Log(
                    "KeyUp: keeping existing _isPreviewRequest = " +
                    _isPreviewRequest);
            }

            // --------------------------------------------------------
            // InvokeRoutine
            // --------------------------------------------------------

            if (_isPreviewRequest)
            {
                bool shouldInvoke =
                    isKeyDown ||
                    e.KeyCode != Keys.Space ||
                    DateTime.Now.Ticks -
                    _spaceHoldTick >=
                    HOLD_TO_PREVIEW_DURATION;

                Log(
                    "ShouldInvoke = " +
                    shouldInvoke);

                if (shouldInvoke)
                {
                    Log(
                        ">>> InvokeRoutine(" +
                        e.KeyCode +
                        ", " +
                        isKeyDown +
                        ")");

                    InvokeRoutine(
                        e.KeyCode,
                        isKeyDown);

                    Log(
                        "<<< InvokeRoutine");

                    if (isKeyDown &&
                        e.KeyCode == Keys.Space)
                    {
                        _spaceIsDown = true;

                        Log(
                            "_spaceIsDown = TRUE");
                    }
                }
                else
                {
                    Log(
                        "RETURN: Space hold duration not reached.");
                }
            }
            else
            {
                Log(
                    "RETURN/NO INVOKE: _isPreviewRequest == false");
            }

            // --------------------------------------------------------
            // Key release
            // --------------------------------------------------------

            if (!isKeyDown)
            {
                Log(
                    "KeyUp: resetting request state.");

                _isPreviewRequest = false;

                _spaceIsDown =
                    e.KeyCode != Keys.Space &&
                    _spaceIsDown;

                Log(
                    "After KeyUp reset:");

                Log(
                    "_isPreviewRequest = " +
                    _isPreviewRequest);

                Log(
                    "_spaceIsDown = " +
                    _spaceIsDown);
            }

            Log(
                "Processing finished.");
        }

        // ------------------------------------------------------------
        // InvokeRoutine
        // ------------------------------------------------------------

        private void InvokeRoutine(
            Keys key,
            bool isKeyDown)
        {
            Log(
                "InvokeRoutine: key=" +
                key +
                ", down=" +
                isKeyDown);

            if (isKeyDown)
            {
                switch (key)
                {
                    case Keys.Enter:

                        Log(
                            "Sending PipeMessages.RunAndClose");

                        PipeServerManager.SendMessage(
                            PipeMessages.RunAndClose);

                        break;

                    case Keys.Space:

                        Log(
                            "Sending PipeMessages.Toggle DOWN");

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

                        Log(
                            "Sending PipeMessages.Switch");

                        PipeServerManager.SendMessage(
                            PipeMessages.Switch);

                        break;

                    case Keys.Escape:

                        Log(
                            "Sending PipeMessages.Close");

                        PipeServerManager.SendMessage(
                            PipeMessages.Close);

                        break;

                    case Keys.Space:

                        Log(
                            "Sending PipeMessages.Toggle UP");

                        PipeServerManager.SendMessage(
                            PipeMessages.Toggle);

                        break;
                }
            }
        }

        // ------------------------------------------------------------
        // Keyboard hook
        // ------------------------------------------------------------

        private void InstallKeyHook(
            KeyEventHandler downHandler,
            KeyEventHandler upHandler)
        {
            Log(
                "Installing GlobalKeyboardHook...");

            _hook =
                GlobalKeyboardHook.GetInstance();

            _hook.KeyDown += downHandler;
            _hook.KeyUp += upHandler;

            Log(
                "GlobalKeyboardHook installed.");
        }

        // ------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------

        internal static KeystrokeDispatcher GetInstance()
        {
            if (_instance == null)
            {
                Log(
                    "Creating KeystrokeDispatcher singleton.");

                _instance =
                    new KeystrokeDispatcher();
            }

            return _instance;
        }
    }
}
