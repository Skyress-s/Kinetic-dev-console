﻿using System;
using UnityEngine;

namespace DavidFDev.DevConsole
{
    /// <summary>
    ///     Interface for accessing the developer console.
    /// </summary>
    public static class DevConsole
    {
        #region Static fields and constants

        private static DevConsoleMono _console;

        #endregion

        #region Static properties

        /// <summary>
        ///     Whether the dev console is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get => _console.consoleIsEnabled;
            set
            {
                if (value)
                {
                    EnableConsole();
                    return;
                }

                DisableConsole();
            }
        }

        /// <summary>
        ///     Whether the dev console is open.
        /// </summary>
        public static bool IsOpen
        {
            get => _console.consoleIsShowing;
            set
            {
                if (value)
                {
                    _console.OpenConsole();
                    return;
                }

                _console.CloseConsole();
            }
        }

        #endregion

        #region Static methods

        /// <summary>
        ///     Add a command to the dev console database.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="onlyInDevBuild"></param>
        /// <returns></returns>
        public static bool AddCommand(Command command, bool onlyInDevBuild = false)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return _console.AddCommand(command, onlyInDevBuild);
        }

        /// <summary>
        ///     Run a command using the provided input.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool RunCommand(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            return _console.RunCommand(input);
        }

        /// <summary>
        ///     Log a message to the dev console.
        /// </summary>
        /// <param name="message"></param>
        public static void Log(object message)
        {
            _console.Log(message);
        }

        /// <summary>
        ///     Log a message to the dev console.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="colour"></param>
        public static void Log(object message, Color colour)
        {
            _console.Log(message, ColorUtility.ToHtmlStringRGBA(colour));
        }

        /// <summary>
        ///     Log a variable to the dev console.
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        public static void LogVariable(string variableName, object value)
        {
            _console.LogVariable(variableName, value);
        }

        /// <summary>
        ///     Log an error message to the dev console.
        /// </summary>
        /// <param name="message"></param>
        public static void LogError(object message)
        {
            _console.LogError(message);
        }

        /// <summary>
        ///     Log a warning message to the dev console.
        /// </summary>
        /// <param name="message"></param>
        public static void LogWarning(object message)
        {
            _console.LogWarning(message);
        }

        /// <summary>
        ///     Log a success message to the dev console.
        /// </summary>
        /// <param name="message"></param>
        public static void LogSuccess(object message)
        {
            _console.LogSuccess(message);
        }

        /// <summary>
        ///     Log the most recently executed command syntax to the dev console.
        /// </summary>
        public static void LogCommand()
        {
            _console.LogCommand();
        }

        /// <summary>
        ///     Log command syntax to the dev console.
        /// </summary>
        /// <param name="name"></param>
        public static void LogCommand(string name)
        {
            _console.LogCommand(name);
        }

        /// <summary>
        ///     Set the key used to toggle the dev console, NULL if no key.
        /// </summary>
        /// <param name="toggleKey"></param>
        public static void SetToggleKey(KeyCode? toggleKey)
        {
            _console.consoleToggleKey = toggleKey;
        }

        /// <summary>
        ///     Enable the dev console.
        /// </summary>
        public static void EnableConsole()
        {
            _console.EnableConsole();
        }

        /// <summary>
        ///     Disable the dev console, making it inaccessible.
        /// </summary>
        public static void DisableConsole()
        {
            _console.DisableConsole();
        }

        /// <summary>
        ///     Open the dev console window.
        /// </summary>
        public static void OpenConsole()
        {
            _console.OpenConsole();
        }

        /// <summary>
        ///     Close the dev console window.
        /// </summary>
        public static void CloseConsole()
        {
            _console.CloseConsole();
        }

        /// <summary>
        ///     Clear the contents of the dev console.
        /// </summary>
        public static void ClearConsole()
        {
            _console.ClearConsole();
        }

        /// <summary>
        ///     Register a callback for when the dev console is opened.
        /// </summary>
        /// <param name="callback"></param>
        public static void Register_OnDevConsoleOpened(Action callback)
        {
            _console.OnDevConsoleOpened += callback;
        }

        /// <summary>
        ///     Deregister a callback for when the dev console is opened.
        /// </summary>
        /// <param name="callback"></param>
        public static void Deregister_OnDevConsoleOpened(Action callback)
        {
            _console.OnDevConsoleOpened -= callback;
        }

        /// <summary>
        ///     Register a callback for when the dev console is closed.
        /// </summary>
        /// <param name="callback"></param>
        public static void Register_OnDevConsoleClosed(Action callback)
        {
            _console.OnDevConsoleClosed += callback;
        }

        /// <summary>
        ///     Deregister a callback for when the dev console is closed.
        /// </summary>
        /// <param name="callback"></param>
        public static void Deregister_OnDevConsoleClosed(Action callback)
        {
            _console.OnDevConsoleClosed -= callback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#pragma warning disable IDE0051
        private static void Init()
#pragma warning restore IDE0051
        {
            GameObject obj = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("FAB_DevConsoleInstance"));
            _console = obj.GetComponent<DevConsoleMono>();
        }

        #endregion
    }
}