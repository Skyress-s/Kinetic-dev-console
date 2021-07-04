﻿// File: DevConsoleMono.cs
// Purpose: Implementation of the developer console as an internal component
// Created by: DavidFDev

// Hide the dev console objects in the hierarchy and inspector
#define HIDE_FROM_EDITOR

#if INPUT_SYSTEM_INSTALLED && ENABLE_INPUT_SYSTEM
#define USE_NEW_INPUT_SYSTEM
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Runtime.CompilerServices;
#if INPUT_SYSTEM_INSTALLED
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

using InputKey =
#if USE_NEW_INPUT_SYSTEM
    UnityEngine.InputSystem.Key;
#else
    UnityEngine.KeyCode;
#endif

namespace DavidFDev.DevConsole
{
#if HIDE_FROM_EDITOR
    [AddComponentMenu("")]
#endif
    internal sealed class DevConsoleMono : MonoBehaviour
    {
        #region Static fields and constants

        private const string ErrorColour = "#E99497";
        private const string WarningColour = "#B3E283";
        private const string SuccessColour = "#B3E283";
        private const string ClearLogText = "Type <b>devconsole</b> for instructions on how to use the developer console.";
        private const int MaximumTextVertices = 64000;
        private const float MinConsoleWidth = 650;
        private const float MaxConsoleWidth = 1200;
        private const float MinConsoleHeight = 200;
        private const float MaxConsoleHeight = 900;
        private const int CommandHistoryLength = 10;
        private const InputKey DefaultToggleKey =
#if USE_NEW_INPUT_SYSTEM
            InputKey.Backquote;
#else
            InputKey.BackQuote;
#endif
        private const InputKey UpArrowKey = InputKey.UpArrow;
        private const InputKey DownArrowKey = InputKey.DownArrow;
        private const string InputSystemPrefabPath = "Prefabs/" +
#if USE_NEW_INPUT_SYSTEM
            "FAB_DevConsole.NewEventSystem";
#else
            "FAB_DevConsole.OldEventSystem";
#endif

        private static readonly Version _version = new Version(0, 1, 5);
        private static readonly string[] _permanentCommands =
        {
            "devconsole", "commands", "help", "print", "clear", "reset"
        };

        #endregion

        #region Fields

        #region Serialised fields

        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private Text _versionText = null;

        [Header("Input")]
        [SerializeField] private InputField _inputField = null;
        [SerializeField] private Text _suggestionText = null;

        [Header("Logs")]
        [SerializeField] private GameObject _logFieldPrefab = null;
        [SerializeField] private RectTransform _logContentTransform = null;

        [Header("Window")]
        [SerializeField] private RectTransform _dynamicTransform = null;
        [SerializeField] private Image _resizeButtonImage = null;
        [SerializeField] private Color _resizeButtonHoverColour = default;

        #endregion

        private bool _init = false;

        #region Input fields

        private bool _focusInputField = false;

        #endregion

        #region Log fields

        private readonly List<InputField> _logFields = new List<InputField>();
        private string _logTextStore = "";
        private readonly TextGenerator _textGenerator = new TextGenerator();
        private int _vertexCount = 0;

        #endregion

        #region Window fields

        private bool _repositioning = false;
        private Vector2 _initPosition = default;
        private Vector2 _repositionOffset = default;
        private bool _resizing = false;
        private Vector2 _initSize = default;
        private Color _resizeButtonColour = default;
        private float _initLogFieldWidth = 0f;
        private float _currentLogFieldWidth = 0f;

        #endregion

        #region Command fields

        private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>();
        private readonly Dictionary<Type, Func<string, object>> _parameterParseFuncs = new Dictionary<Type, Func<string, object>>();
        private readonly List<string> _commandHistory = new List<string>(CommandHistoryLength);
        private string _lastCommand = string.Empty;
        private int _commandHistoryIndex = -1;
        private bool _displayUnityLogs = true;
        private bool _displayUnityErrors = true;
        private bool _displayUnityExceptions = true;
        private bool _displayUnityWarnings = true;
        private string[] _commandSuggestions = null;
        private int _commandSuggestionIndex = 0;
        private bool _ignoreInputChange = false;

        #endregion

        #endregion

        #region Properties

        internal InputKey? ConsoleToggleKey { get; private set; } = DefaultToggleKey;

        internal bool ConsoleIsEnabled { get; private set; }

        internal bool ConsoleIsShowing { get; private set; }

        internal bool ConsoleIsShowingAndFocused => ConsoleIsShowing && _inputField.isFocused;

        private string InputText
        {
            get => _inputField.text;
            set => _inputField.text = value;
        }

        private int InputCaretPosition
        {
            get => _inputField.caretPosition;
            set => _inputField.caretPosition = value;
        }

        #endregion

        #region Events

        internal event Action OnDevConsoleOpened;

        internal event Action OnDevConsoleClosed;

        #endregion

        #region Methods

        #region Console methods

        internal void EnableConsole()
        {
            if (!_init && ConsoleIsEnabled)
            {
                return;
            }

            Application.logMessageReceived += OnLogMessageReceived;
            //Application.logMessageReceivedThreaded += OnLogMessageReceived;
            ClearConsole();
            InputText = string.Empty;
            ConsoleIsEnabled = true;
            enabled = true;
        }

        internal void DisableConsole()
        {
            if (!_init && !ConsoleIsEnabled)
            {
                return;
            }

            if (ConsoleIsShowing)
            {
                CloseConsole();
            }
            _dynamicTransform.anchoredPosition = _initPosition;
            _dynamicTransform.sizeDelta = _initSize;
            _commandHistory.Clear();
            ClearConsole();
            Application.logMessageReceived -= OnLogMessageReceived;
            //Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            ConsoleIsEnabled = false;
            enabled = false;
        }

        internal void OpenConsole()
        {
            if (!_init && (!ConsoleIsEnabled || ConsoleIsShowing))
            {
                return;
            }

            // Create a new event system if none exists
            if (EventSystem.current == null)
            {
                GameObject obj = Instantiate(Resources.Load<GameObject>(InputSystemPrefabPath));
                EventSystem.current = obj.GetComponent<EventSystem>();
                obj.name = "EventSystem";
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            ConsoleIsShowing = true;
            _focusInputField = true;
            InputText = InputText.TrimEnd('`');

            OnDevConsoleOpened?.Invoke();
        }

        internal void CloseConsole()
        {
            if (!_init && (!ConsoleIsEnabled || !ConsoleIsShowing))
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            ConsoleIsShowing = false;
            _repositioning = false;
            _resizing = false;

            OnDevConsoleClosed?.Invoke();
        }

        internal void ToggleConsole()
        {
            if (ConsoleIsShowing)
            {
                CloseConsole();
                return;
            }

            OpenConsole();
        }

        internal void SetToggleKey(InputKey? toggleKey)
        {
            ConsoleToggleKey = toggleKey;
        }

        internal void ClearConsole()
        {
            ClearLogFields();
            _vertexCount = 0;
            _logTextStore = ClearLogText;
        }

        internal void SubmitInput()
        {
            if (!string.IsNullOrWhiteSpace(InputText))
            {
                RunCommand(InputText);
            }

            InputText = string.Empty;
        }

        internal bool RunCommand(string rawInput)
        {
            // Get the input as an array
            // First element is the command name
            // Remainder are raw parameters
            string[] input = GetInput(rawInput);

            // Find the command
            Command command = GetCommand(input[0]);

            // Add the input to the command history, even if it isn't a valid command
            AddToCommandHistory(input[0], rawInput);

            if (command == null)
            {
                LogError($"Could not find the specified command: \"{input[0]}\".");
                return false;
            }

            // Determine the actual parameters now that we know the expected parameters
            input = ConvertInput(input, command.Parameters.Length);

            // Try to execute the default callback if the command has no parameters specified
            if (input.Length == 1 && command.DefaultCallback != null)
            {
                command.DefaultCallback();
                return true;
            }

            if (command.Parameters.Length != input.Length - 1)
            {
                LogError($"Invalid number of parameters: {command.ToFormattedString()}.");
                return false;
            }

            // Iterate through the parameters and convert to the appropriate type
            object[] parameters = new object[command.Parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                string parameter = input[i + 1];

                try
                {
                    // Allow bools to be in the form of "0" and "1"
                    if (command.Parameters[i].Type == typeof(bool) && int.TryParse(parameter, out int result))
                    {
                        if (result == 0)
                        {
                            parameter = "false";
                        }
                        else if (result == 1)
                        {
                            parameter = "true";
                        }
                    }

                    // Try to convert the parameter input into the appropriate type
                    parameters[i] = ParseParameter(parameter, command.Parameters[i].Type);
                }
                catch (Exception)
                {
                    LogError($"Invalid parameter type: \"{parameter}\". Expected {command.GetFormattedParameter(i)}.");
                    return false;
                }
            }

            // Execute the command callback with the parameters, if any
            command.Callback(parameters.Length == 0 ? null : parameters);
            return true;
        }

        internal bool AddCommand(Command command, bool onlyInDevBuild = false)
        {
            if (onlyInDevBuild && !Debug.isDebugBuild)
            {
                return false;
            }

            // Try to fix the command name, removing any whitespace and converting it to lowercase
            command.FixName();

            // Try to fix the aliases in the same manner
            command.FixAliases();

            // Try to add the command, making sure it doesn't conflict with any other commands
            if (!string.IsNullOrEmpty(command.Name) && !_commands.ContainsKey(command.Name) && !_commands.Values.Select(c => c.Aliases).Any(a => command.HasAlias(a)))
            {
                _commands.Add(command.Name, command);
                return true;
            }
            return false;
        }

        internal bool RemoveCommand(string name)
        {
            Command command = GetCommand(name);

            if (command == null)
            {
                return true;
            }

            if (_permanentCommands.Contains(name))
            {
                return false;
            }

            return _commands.Remove(command.Name);
        }

        internal bool AddParameterType(Type type, Func<string, object> parseFunc)
        {
            // Try to add the parameter type, if one doesn't already exist for this type
            if (!_parameterParseFuncs.ContainsKey(type))
            {
                _parameterParseFuncs.Add(type, parseFunc);
                return true;
            }
            return false;
        }

        #endregion

        #region Log methods

        internal void Log(object message)
        {
            _logTextStore += $"\n{message}";
        }

        internal void Log(object message, string htmlColour)
        {
            Log($"<color={htmlColour}>{message}</color>");
        }

        internal void LogVariable(string variableName, object value)
        {
            Log($"{variableName}: {value}.");
        }

        internal void LogError(object message)
        {
            Log(message, ErrorColour);
        }

        internal void LogWarning(object message)
        {
            Log(message, WarningColour);
        }

        internal void LogSuccess(object message)
        {
            Log(message, SuccessColour);
        }

        internal void LogSeperator(object message = null)
        {
            if (message == null)
            {
                Log("-");
            }
            else
            {
                Log($"- <b>{message}</b> -");
            }
        }

        internal void LogCommand()
        {
            LogCommand(_lastCommand);
        }

        internal void LogCommand(string name)
        {
            Command command = GetCommand(name);
            if (command != null)
            {
                Log($">> {command.ToFormattedString()}.");
            }
        }

        #endregion

        #region Unity events

        internal void OnInputValueChanged()
        {
            if (_ignoreInputChange)
            {
                return;
            }

            _ignoreInputChange = true;

            // Submit the input if a new line is entered (ENTER)
            if (InputText.Contains("\n"))
            {
                InputText = InputText.Replace("\n", string.Empty);
                SubmitInput();
            }

            // Try autocomplete if tab is entered (TAB)
            else if (InputText.Contains("\t"))
            {
                InputText = InputText.Replace("\t", string.Empty);
                AutoComplete();
            }

            RefreshCommandSuggestions();
            _ignoreInputChange = false;
        }

        internal void OnRepositionButtonPointerDown(BaseEventData eventData)
        {
            _repositioning = true;
            _repositionOffset = ((PointerEventData)eventData).position - (Vector2)_dynamicTransform.position;
        }

        internal void OnRepositionButtonPointerUp(BaseEventData _)
        {
            _repositioning = false;
        }

        internal void OnResizeButtonPointerDown(BaseEventData _)
        {
            _resizing = true;
        }

        internal void OnResizeButtonPointerUp(BaseEventData _)
        {
            _resizing = false;
            _resizeButtonImage.color = _resizeButtonColour;
            RefreshLogFieldsSize();
        }

        internal void OnResizeButtonPointerEnter(BaseEventData _)
        {
            _resizeButtonImage.color = _resizeButtonColour * _resizeButtonHoverColour;
        }

        internal void OnResizeButtonPointerExit(BaseEventData _)
        {
            if (!_resizing)
            {
                _resizeButtonImage.color = _resizeButtonColour;
            }
        }

        internal void OnAuthorButtonPressed()
        {
#if !UNITY_ANDROID && !UNITY_IOS
            Application.OpenURL(@"https://www.davidfdev.com");
#endif
        }

        #endregion

        #region Unity methods

        private void Awake()
        {
            _init = true;

            // Set up the game object
            gameObject.name = "DevConsoleInstance";
            DontDestroyOnLoad(gameObject);

#if HIDE_FROM_EDITOR
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
#endif

            _versionText.text = "v" + _version.ToString();
            _initPosition = _dynamicTransform.anchoredPosition;
            _initSize = _dynamicTransform.sizeDelta;
            _initLogFieldWidth = _logFieldPrefab.GetComponent<RectTransform>().sizeDelta.x;
            _currentLogFieldWidth = _initLogFieldWidth;
            _resizeButtonColour = _resizeButtonImage.color;
            _logFieldPrefab.SetActive(false);

            InitPreferences();
            InitBuiltInCommands();
            InitAttributeCommands();

            // Enable the console by default if in editor or a development build
            if (Debug.isDebugBuild)
            {
                EnableConsole();
            }
            else
            {
                DisableConsole();
            }

            ClearConsole();
            CloseConsole();

            _init = false;
        }

        private void Update()
        {
            if (!ConsoleIsEnabled || !ConsoleIsShowing)
            {
                return;
            }

            // Force the input field to be focused by the event system
            if (_focusInputField)
            {
                EventSystem.current.SetSelectedGameObject(_inputField.gameObject, null);
                _focusInputField = false;
            }

            // Move the developer console using the mouse position
            if (_repositioning)
            {
                Vector2 mousePosition = GetMousePosition();
                _dynamicTransform.position = new Vector3(
                    mousePosition.x - _repositionOffset.x,
                    mousePosition.y - _repositionOffset.y,
                    _dynamicTransform.position.z);
            }

            // Resize the developer console using the mouse position
            if (_resizing)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_dynamicTransform, GetMousePosition(), null, out Vector2 localPoint);
                localPoint.x = Mathf.Clamp(Mathf.Abs(localPoint.x), MinConsoleWidth, MaxConsoleWidth);
                localPoint.y = Mathf.Clamp(Mathf.Abs(localPoint.y), MinConsoleHeight, MaxConsoleHeight);
                _dynamicTransform.sizeDelta = localPoint;

                // Resize the log field too, because Unity refuses to do it automatically
                _currentLogFieldWidth = _initLogFieldWidth * (_dynamicTransform.sizeDelta.x / _initSize.x);
            }
        }

        private void LateUpdate()
        {
            if (!ConsoleIsEnabled)
            {
                return;
            }

            // Force the canvas to rebuild layouts, which will display the log correctly
            if (_logTextStore != string.Empty)
            {
                ProcessStoredLogs();
            }

            // Check if the developer console toggle key was pressed
            if (ConsoleToggleKey.HasValue && (!ConsoleIsShowing || !_inputField.isFocused) && GetKeyDown(ConsoleToggleKey.Value))
            {
                ToggleConsole();
                return;
            }

            if (_inputField.isFocused)
            {
                // Allow cycling through command suggestions using the UP and DOWN arrows
                if (_commandSuggestions != null && _commandSuggestions.Length > 0)
                {
                    if (GetKeyDown(UpArrowKey))
                    {
                        CycleCommandSuggestions(1);
                    }
                    else if (GetKeyDown(DownArrowKey))
                    {
                        CycleCommandSuggestions(-1);
                    }
                }

                // Allow cycling through command history using the UP and DOWN arrows
                else
                {
                    if (_commandHistoryIndex != -1 && InputText == string.Empty)
                    {
                        _commandHistoryIndex = -1;
                    }

                    if (GetKeyDown(UpArrowKey))
                    {
                        CycleCommandHistory(1);
                    }
                    else if (GetKeyDown(DownArrowKey))
                    {
                        CycleCommandHistory(-1);
                    }
                }
            }
        }

        private void OnDestroy()
        {
#if USE_NEW_INPUT_SYSTEM
            // TODO: Save console toggle key in new input system
#else
            PlayerPrefs.SetInt("DevConsole.legacyConsoleToggleKey", !ConsoleToggleKey.HasValue ? -1 : (int)ConsoleToggleKey.Value);
#endif
            PlayerPrefs.SetInt("DevConsole.displayUnityLogs", _displayUnityLogs ? 1 : 0);
            PlayerPrefs.SetInt("DevConsole.displayUnityErrors", _displayUnityErrors ? 1 : 0);
            PlayerPrefs.SetInt("DevConsole.displayUnityExceptions", _displayUnityExceptions ? 1 : 0);
            PlayerPrefs.SetInt("DevConsole.displayUnityWarnings", _displayUnityWarnings ? 1 : 0);

            PlayerPrefs.Save();
        }

        #endregion

        #region Init methods

        private void InitPreferences()
        {
#if USE_NEW_INPUT_SYSTEM
            // TODO: Load console toggle key in new input system
#else
            int n = PlayerPrefs.GetInt("DevConsole.legacyConsoleToggleKey", (int)DefaultToggleKey);
            ConsoleToggleKey = n < 0 ? (InputKey?)null : (InputKey)n;
#endif
            _displayUnityLogs = PlayerPrefs.GetInt("DevConsole.displayUnityLogs", 1) == 1;
            _displayUnityErrors = PlayerPrefs.GetInt("DevConsole.displayUnityErrors", 1) == 1;
            _displayUnityExceptions = PlayerPrefs.GetInt("DevConsole.displayUnityExceptions", 1) == 1;
            _displayUnityWarnings = PlayerPrefs.GetInt("DevConsole.displayUnityWarnings", 1) == 1;
        }

        private void InitBuiltInCommands()
        {
            #region Console commands

            AddCommand(Command.Create(
                "devconsole",
                "",
                "Display instructions on how to use the developer console",
                () =>
                {
                    LogSeperator($"Developer console (v{_version})");
                    Log("Use <b>commands</b> to display a list of available commands.");
                    Log($"Use {GetCommand("help").ToFormattedString()} to display information about a specific command.");
                    Log("Use UP / DOWN to cycle through command history or suggested commands.");
                    Log("Use TAB to autocomplete a suggested command.");
                    Log("");
                    Log("Created by @DavidF_Dev.");
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<string>(
                "print",
                "echo",
                "Display a message in the developer console",
                Parameter.Create("message", "Message to display"),
                s => Log(s)
            ));

            AddCommand(Command.Create(
                "clear",
                "",
                "Clear the developer console",
                () => ClearConsole()
            ));

            AddCommand(Command.Create(
                "reset",
                "",
                "Reset the position and size of the developer console",
                () =>
                {
                    _dynamicTransform.anchoredPosition = _initPosition;
                    _dynamicTransform.sizeDelta = _initSize;
                    _currentLogFieldWidth = _initLogFieldWidth;
                    RefreshLogFieldsSize();
                }
            ));

            AddCommand(Command.Create(
                "closeconsole",
                "hideconsole",
                "Close the developer console window",
                () => CloseConsole()
            ));

            AddCommand(Command.Create<string>(
                "help",
                "info",
                "Display information about a specified command",
                Parameter.Create(
                    "commandName",
                    "Name of the command to get information about"),
                s =>
                {
                    Command command = GetCommand(s);

                    if (command == null)
                    {
                        LogError($"Unknown command name specified: \"{s}\". Use <b>list</b> for a list of all commands.");
                        return;
                    }

                    LogSeperator(command.Name);

                    if (!string.IsNullOrEmpty(command.HelpText))
                    {
                        Log(command.HelpText + ".");
                    }

                    if (command.Aliases?.Length > 0 && command.Aliases.Any(a => !string.IsNullOrEmpty(a)))
                    {
                        string[] formattedAliases = command.Aliases.Select(alias => $"<i>{alias}</i>").ToArray();
                        Log($"Aliases: {string.Join(", ", formattedAliases)}.");
                    }

                    if (command.Parameters.Length > 0)
                    {
                        Log($"Syntax: {command.ToFormattedString()}.");
                    }

                    foreach (Parameter parameter in command.Parameters)
                    {
                        if (!string.IsNullOrEmpty(parameter.HelpText))
                        {
                            Log($" <b>{parameter.Name}</b>: {parameter.HelpText}.");
                        }
                    }

                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "commands",
                "",
                "Display a sorted list of all available commands",
                () =>
                {
                    LogSeperator("Commands");
                    Log(string.Join(", ", _commands.Keys.OrderBy(s => s)));
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "consoleversion",
                "",
                "Display the developer console version",
                () => Log($"Developer console version: {_version}.")
            ));

            #endregion

            #region Player commands

            AddCommand(Command.Create(
                "quit",
                "exit",
                "Exit the player application",
                () =>
                {
                    if (Application.isEditor)
                    {
                        LogError("Cannot quit the player application when running in the Editor.");
                        return;
                    }

                    Application.Quit();
                }
            ));

            AddCommand(Command.Create(
                "appversion",
                "",
                "Display the application version",
                () => Log($"App version: {Application.version}.")
            ));

            AddCommand(Command.Create(
                "unityversion",
                "",
                "Display the engine version",
                () => Log($"Engine version: {Application.unityVersion}.")
            ));

            AddCommand(Command.Create(
                "unityinput",
                "",
                "Display the input system being used by the developer console",
                () =>
                {
#if USE_NEW_INPUT_SYSTEM
                    Log("The new input system package is currently being used.");
#else
                    Log("The legacy input system is currently being used.");
#endif
                }
            ));

            AddCommand(Command.Create(
                "path",
                "",
                "Display the path to the application executable",
                () => Log($"Application path: {AppDomain.CurrentDomain.BaseDirectory}.")
            ));

            #endregion

            #region Screen commands

            AddCommand(Command.Create<bool>(
                "fullscreen",
                "",
                "Query or set whether the window is fullscreen",
                Parameter.Create("enabled", "Whether the window is fullscreen"),
                b =>
                {
                    Screen.fullScreen = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} fullscreen mode.");
                },
                () => LogVariable("Fullscreen", Screen.fullScreen)
            ));

            AddCommand(Command.Create<int>(
                "vsync",
                "",
                "Query or set whether VSync is enabled",
                Parameter.Create("vSyncCount", "The number of VSyncs that should pass between each frame (0, 1, 2, 3, or 4)."),
                i =>
                {
                    if (i < 0 || i > 4)
                    {
                        LogError($"Provided VSyncCount is not an accepted value: \"{i}\".");
                        return;
                    }

                    QualitySettings.vSyncCount = i;
                    LogSuccess($"VSyncCount set to {i}.");
                },
                () => LogVariable("VSyncCount", QualitySettings.vSyncCount)
            ));

            AddCommand(Command.Create(
                "resolution",
                "",
                "Display the current screen resolution",
                () => LogVariable("Resolution", Screen.currentResolution)
            ));

            AddCommand(Command.Create<int>(
                "fps_target",
                "fps_max",
                "Query or set the target frame rate.",
                Parameter.Create("targetFrameRate", "Frame rate the application will try to render at."),
                i =>
                {
                    Application.targetFrameRate = i;
                    LogSuccess($"Target frame rate set to {i}.");
                },
                () => LogVariable("TargetFrameRate", Application.targetFrameRate)
            ));

            #endregion

            #region Camera commands

            AddCommand(Command.Create<bool>(
                "cam_ortho",
                "",
                "Query or set whether the main camera is orthographic",
                Parameter.Create("enabled", "Whether the main camera is orthographic"),
                b =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    Camera.main.orthographic = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} orthographic mode on the main camera.");
                },
                () =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    LogVariable("Orthographic", Camera.main.orthographic);
                }
            ));

            AddCommand(Command.Create<int>(
                "cam_fov",
                "",
                "Query or set the main camera field of view",
                Parameter.Create("fieldOfView", "Field of view"),
                f =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    Camera.main.fieldOfView = f;
                    LogSuccess($"Main camera's field of view set to {f}.");
                },
                () =>
                {
                    if (Camera.main == null)
                    {
                        LogError("Could not find the main camera.");
                        return;
                    }

                    LogVariable("FieldOfView", Camera.main.fieldOfView);
                }
            ));

            #endregion

            #region Scene commands

            AddCommand(Command.Create<int>(
                "scene_load",
                "",
                "Load the scene at the specified build index",
                Parameter.Create(
                    "buildIndex",
                    "Build index of the scene to load, specified in the Unity build settings"
                    ),
                i =>
                {
                    if (i >= SceneManager.sceneCountInBuildSettings)
                    {
                        LogError($"Invalid build index specified: \"{i}\". Check the Unity build settings.");
                        return;
                    }

                    SceneManager.LoadScene(i);
                    LogSuccess($"Loaded scene at build index {i}.");
                }
            ), true);

            AddCommand(Command.Create<int>(
                "scene_info",
                "",
                "Display information about the current scene",
                Parameter.Create("sceneIndex", "Index of the scene in the currently loaded scenes"),
                i =>
                {
                    if (i >= SceneManager.sceneCount)
                    {
                        LogError($"Could not find active scene at index: {i}.");
                        return;
                    }

                    Scene scene = SceneManager.GetSceneAt(i);
                    LogSeperator(scene.name);
                    Log($"Scene index: {i}.");
                    Log($"Build index: {scene.buildIndex}.");
                    Log($"Path: {scene.path}.");
                    LogSeperator();
                },
                () =>
                {
                    if (SceneManager.sceneCount == 0)
                    {
                        Log("Could not find any active scenes.");
                        return;
                    }

                    LogSeperator("Active scenes");
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
                        Log($" {i}) {scene.name}, build index: {scene.buildIndex}.");
                    }
                    LogCommand();
                    LogSeperator();
                }
            ));

            AddCommand(Command.Create<string>(
                "obj_info",
                "",
                "Display information about a game object in the scene",
                Parameter.Create("name", "Name of the game object"),
                s =>
                {
                    GameObject obj = GameObject.Find(s);

                    if (obj == null)
                    {
                        LogError($"Could not find game object: \"{s}\".");
                        return;
                    }

                    LogSeperator($"{obj.name} ({(obj.activeInHierarchy ? "enabled" : " disabled")})");
                    if (obj.TryGetComponent(out RectTransform rect))
                    {
                        Log("RectTransform:");
                        LogVariable(" Anchored position", rect.anchoredPosition);
                        LogVariable(" Size", rect.sizeDelta);
                        LogVariable(" Pivot", rect.pivot);
                    }
                    else
                    {
                        Log("Transform:");
                        LogVariable(" Position", obj.transform.position);
                        LogVariable(" Rotation", obj.transform.rotation);
                        LogVariable(" Scale", obj.transform.localScale);
                    }
                    LogVariable("Tag", obj.tag);
                    LogVariable("Physics layer", LayerMask.LayerToName(obj.layer));

                    Component[] components = obj.GetComponents(typeof(Component));
                    if (components.Length > 1)
                    {
                        Log("Components:");
                        for (int i = 1; i < components.Length; i++)
                        {
                            if (components[i] is MonoBehaviour mono)
                            {
                                Log($" {i}: {mono.GetType().Name} ({(mono.enabled ? "enabled" : "disabled")}).");
                            }
                            else
                            {
                                Log($" {i}: {components[i].GetType().Name}.");
                            }
                        }
                    }

                    if (obj.transform.childCount > 0)
                    {
                        Log("Children:");
                        Transform child;
                        for (int i = 0; i < obj.transform.childCount; i++)
                        {
                            child = obj.transform.GetChild(i);
                            Log($" {i}: {child.gameObject.name} ({(child.gameObject.activeInHierarchy ? "enabled" : "disabled")}).");
                        }
                    }

                    LogSeperator();
                }
            ));

            AddCommand(Command.Create(
                "obj_list",
                "",
                "Display a hierarchical list of all game objects in the scene",
                () =>
                {
                    GameObject[] root = SceneManager.GetActiveScene().GetRootGameObjects();
                    Transform t;
                    string logResult = string.Empty;
                    const int space = 2;

                    string getTabbed(int tabAmount)
                    {
                        string tabbed = string.Empty;
                        for (int i = 0; i < tabAmount; i++)
                        {
                            tabbed += (i % space == 0) ? '|' : ' ';
                        }
                        return tabbed;
                    }

                    void logChildren(GameObject obj, int tabAmount)
                    {
                        string tabbed = getTabbed(tabAmount);
                        for (int i = 0; i < obj.transform.childCount; i++)
                        {
                            t = obj.transform.GetChild(i);
                            logResult += $"{tabbed}{t.gameObject.name}.\n";
                            logChildren(t.gameObject, tabAmount + 2);
                        }
                    }

                    foreach (GameObject rootObj in root)
                    {
                        logResult += $"{rootObj.gameObject.name}.\n";
                        logChildren(rootObj, space);
                    }

                    LogSeperator($"Hierarchy ({SceneManager.GetActiveScene().name})");
                    Log(logResult.TrimEnd('\n'));
                    LogSeperator();
                }
            ));

            #endregion

            #region Log commands

            AddCommand(Command.Create<bool>(
                "log_logs",
                "",
                "Query, enable or disable displaying Unity logs in the developer console",
                Parameter.Create("enabled", "Whether Unity logs should be displayed in the developer console"),
                b =>
                {
                    _displayUnityLogs = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} displaying Unity logs in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity logs", _displayUnityLogs);
                }
            ));

            AddCommand(Command.Create<bool>(
                "log_errors",
                "",
                "Query, enable or disable displaying Unity errors in the developer console",
                Parameter.Create("enabled", "Whether Unity errors should be displayed in the developer console"),
                b =>
                {
                    _displayUnityErrors = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} displaying Unity errors in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity errors", _displayUnityErrors);
                }
            ));

            AddCommand(Command.Create<bool>(
                "log_exceptions",
                "",
                "Query, enable or disable displaying Unity exceptions in the developer console",
                Parameter.Create("enabled", "Whether Unity exceptions should be displayed in the developer console"),
                b =>
                {
                    _displayUnityExceptions = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} displaying Unity exceptions in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity exceptions", _displayUnityExceptions);
                }
            ));

            AddCommand(Command.Create<bool>(
                "log_warnings",
                "",
                "Query, enable or disable displaying Unity warnings in the developer console",
                Parameter.Create("enabled", "Whether Unity warnings should be displayed in the developer console"),
                b =>
                {
                    _displayUnityWarnings = b;
                    LogSuccess($"{(b ? "Enabled" : "Disabled")} displaying Unity warnings in the developer console.");
                },
                () =>
                {
                    LogVariable("Log unity warnings", _displayUnityWarnings);
                }
            ));

            #endregion

            #region Misc commands

            AddCommand(Command.Create(
                "time",
                "",
                "Display the current time",
                () => Log($"Current time: {DateTime.Now}.")
            ));

            #endregion
        }

        private void InitAttributeCommands()
        {
            // https://github.com/yasirkula/UnityIngameDebugConsole/blob/master/Plugins/IngameDebugConsole/Scripts/DebugLogConsole.cs
            // Implementation of finding attributes sourced from yasirkula's code

#if UNITY_EDITOR || !NETFX_CORE
            string[] ignoredAssemblies = new string[]
            {
                "Unity",
                "System",
                "Mono.",
                "mscorlib",
                "netstandard",
                "TextMeshPro",
                "Microsoft.GeneratedCode",
                "I18N",
                "Boo.",
                "UnityScript.",
                "ICSharpCode.",
                "ExCSS.Unity",
#if UNITY_EDITOR
				"Assembly-CSharp-Editor",
                "Assembly-UnityScript-Editor",
                "nunit.",
                "SyntaxTree.",
                "AssetStoreTools"
#endif
            };
#endif

#if UNITY_EDITOR || !NETFX_CORE
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
#else
            foreach (Assembly assembly in new Assembly[] { typeof(DebugConsoleMono).Assembly })
#endif
            {
#if (NET_4_6 || NET_STANDARD_2_0) && (UNITY_EDITOR || !NETFX_CORE)
                if (assembly.IsDynamic)
                    continue;
#endif

                string assemblyName = assembly.GetName().Name;

#if UNITY_EDITOR || !NETFX_CORE
                if (ignoredAssemblies.Any(a => assemblyName.ToLower().StartsWith(a.ToLower())))
                {
                    continue;
                }
#endif

                try
                {
                    foreach (Type type in assembly.GetExportedTypes())
                    {
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            foreach (object attribute in method.GetCustomAttributes(typeof(DevConsoleCommandAttribute), false))
                            {
                                DevConsoleCommandAttribute commandAttribute = (DevConsoleCommandAttribute)attribute;
                                if (commandAttribute != null)
                                {
                                    AddCommand(Command.Create(commandAttribute, method));
                                }
                            }
                        }
                    }
                }
                catch (NotSupportedException) { }
                catch (System.IO.FileNotFoundException) { }
                catch (Exception e)
                {
                    Debug.LogError("Error whilst searching for debug console command attributes in assembly(" + assemblyName + "): " + e.Message + ".");
                }
            }
        }

        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            switch (type)
            {
                case LogType.Log:
                    if (!_displayUnityLogs)
                    {
                        return;
                    }
                    Log($"({time}) <b>Log:</b> {logString}");
                    break;
                case LogType.Error:
                    if (!_displayUnityErrors)
                    {
                        return;
                    }
                    Log($"({time}) <color={ErrorColour}><b>Error:</b> </color>{logString}");
                    break;
                case LogType.Exception:
                    if (!_displayUnityExceptions)
                    {
                        return;
                    }
                    Log($"({time}) <color={ErrorColour}><b>Exception:</b> </color>{logString}");
                    break;
                case LogType.Warning:
                    if (!_displayUnityWarnings)
                    {
                        return;
                    }
                    Log($"({time}) <color={WarningColour}><b>Warning:</b> </color>{logString}");
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Command methods

        private Command GetCommand(string name)
        {
            return _commands.TryGetValue(name.ToLower(), out Command command) ? command : _commands.Values.FirstOrDefault(c => c.HasAlias(name));
        }

        private string[] GetInput(string rawInput)
        {
            string[] split = rawInput.Split(' ');
            if (split.Length <= 1)
            {
                return split;
            }

            List<string> parameters = new List<string>()
            {
                split[0]
            };
            bool buildingParameter = false;
            string parameter = "";
            for (int i = 1; i < split.Length; i++)
            {
                if (!buildingParameter)
                {
                    if (split[i].StartsWith("\"") && i != split.Length - 1)
                    {
                        if (!split[i].EndsWith("\""))
                        {
                            buildingParameter = true;
                            parameter = split[i].TrimStart('"');
                        }
                        else
                        {
                            parameters.Add(split[i].Trim('"'));
                        }
                    }
                    else
                    {
                        parameters.Add(split[i]);
                    }
                }
                else
                {
                    if (split[i].EndsWith("\"") || i == split.Length - 1)
                    {
                        buildingParameter = false;
                        parameter += " " + split[i].TrimEnd('\"');
                        parameters.Add(parameter);
                    }
                    else
                    {
                        parameter += " " + split[i];
                    }
                }
            }

            return parameters.ToArray();
        }

        private string[] ConvertInput(string[] input, int parameterCount)
        {
            if (input.Length - 1 <= parameterCount)
            {
                return input;
            }

            string[] newInput = new string[parameterCount + 1];
            newInput[0] = input[0];
            string aggregatedFinalParameter = "";
            for (int i = 1; i < input.Length; i++)
            {
                if (i - 1 < parameterCount - 1)
                {
                    newInput[i] = input[i];
                }
                else if (i - 1 == parameterCount - 1)
                {
                    aggregatedFinalParameter = input[i];
                }
                else
                {
                    aggregatedFinalParameter += " " + input[i];
                }
            }
            newInput[newInput.Length - 1] = aggregatedFinalParameter;
            return newInput;
        }

        private object ParseParameter(string input, Type type)
        {
            // Check if a parse function exists for the type
            if (_parameterParseFuncs.TryGetValue(type, out Func<string, object> parseFunc))
            {
                return parseFunc(input);
            }

            // Special case if the type is an enum
            if (type.IsEnum)
            {
                object enumParameter;
                if ((enumParameter = Enum.Parse(type, input, true)) != null || (int.TryParse(input, out int enumValue) && (enumParameter = Enum.ToObject(type, enumValue)) != null))
                {
                    return enumParameter;
                }
            }

            // Try to convert as an IConvertible
            return Convert.ChangeType(input, type);
        }

        private void AddToCommandHistory(string name, string input)
        {
            _lastCommand = name;
            _commandHistory.Insert(0, input);
            if (_commandHistory.Count == CommandHistoryLength)
            {
                _commandHistory.RemoveAt(_commandHistory.Count - 1);
            }
            _commandHistoryIndex = -1;
        }

        private void CycleCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0 ||
                (_commandHistoryIndex == _commandHistory.Count - 1 && direction == 1) ||
                (_commandHistoryIndex == -1 && direction == -1))
            {
                return;
            }

            if (_commandHistoryIndex == 0 && direction == -1)
            {
                _commandHistoryIndex = -1;
                InputText = string.Empty;
                return;
            }

            _commandHistoryIndex += direction;
            InputText = _commandHistory[_commandHistoryIndex];
            InputCaretPosition = InputText.Length;
        }

        #endregion

        #region Suggestion methods

        private void RefreshCommandSuggestions()
        {
            // Do not show if there is no command or the parameters are being specified
            if (InputText.Length == 0 || InputText.StartsWith(" ") || InputText.Split(' ').Length > 1 || _commandHistoryIndex != -1)
            {
                _suggestionText.text = string.Empty;
                _commandSuggestions = null;
                _commandSuggestionIndex = 0;
                return;
            }

            _commandSuggestions = GetCommandSuggestions(InputText);
            _commandSuggestionIndex = 0;
            _suggestionText.text = _commandSuggestions.FirstOrDefault() ?? string.Empty;
        }

        private string[] GetCommandSuggestions(string text)
        {
            // Get a list of command names that could fill in the missing text
            List<string> suggestions = new List<string>();
            string textToLower = text.ToLower();
            foreach (string commandName in _commands.Keys)
            {
                if (!commandName.StartsWith(textToLower))
                {
                    continue;
                }

                // Combine current input with suggestion so capitalisation remains
                suggestions.Add(text + commandName.Substring(text.Length));
            }
            return suggestions.ToArray();
        }

        private void AutoComplete()
        {
            if (_commandSuggestions == null || _commandSuggestions.Length == 0)
            {
                return;
            }

            // Complete the input text with the current command suggestion
            InputText = _commandSuggestions[_commandSuggestionIndex];
            InputCaretPosition = InputText.Length;
        }

        private void CycleCommandSuggestions(int direction)
        {
            if (_commandSuggestions == null || _commandSuggestions.Length == 0)
            {
                return;
            }

            // Cycle the command suggestion in the given direction
            _commandSuggestionIndex += direction;
            if (_commandSuggestionIndex < 0)
            {
                _commandSuggestionIndex = _commandSuggestions.Length - 1;
            }
            else if (_commandSuggestionIndex == _commandSuggestions.Length)
            {
                _commandSuggestionIndex = 0;
            }
            _suggestionText.text = _commandSuggestions[_commandSuggestionIndex];
            InputCaretPosition = InputText.Length;
        }

        #endregion

        #region Log content methods

        private void ProcessStoredLogs()
        {
            // Determine number of vertices needed to render the stored logs
            int vertexCountStored = GetVertexCount(_logTextStore);

            // Check if the stored logs exceeds the maximum vertex count
            if (vertexCountStored > MaximumTextVertices)
            {
                // TODO: Split into multiple
                _logTextStore = $"<color={ErrorColour}>Message to log exceeded {MaximumTextVertices} vertices and was ignored.</color>";
                return;
            }

            // Check if the stored logs appended to the current logs exceeds the maximum vertex count
            else if (_vertexCount + vertexCountStored > MaximumTextVertices)
            {
                // Split once
                AddLogField();
                _logFields.Last().text = _logTextStore.TrimStart('\n');
                _vertexCount = vertexCountStored;
            }

            // Otherwise, simply append the stored logs to the current logs
            else
            {
                _logFields.Last().text += _logTextStore;
                _vertexCount += vertexCountStored;
            }

            _logTextStore = string.Empty;
            RebuildLayout();
        }

        private int GetVertexCount(string text)
        {
            Text logText = _logFields.Last().textComponent;
            _textGenerator.Populate(text, logText.GetGenerationSettings(logText.rectTransform.rect.size));
            return _textGenerator.vertexCount;
        }

        private void AddLogField()
        {
            // Instantiate a new log field and set it up with default values
            GameObject obj = Instantiate(_logFieldPrefab, _logContentTransform);
            InputField logField = obj.GetComponent<InputField>();
            logField.text = string.Empty;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(_currentLogFieldWidth, rect.sizeDelta.y);
            _logFields.Add(logField);
            obj.SetActive(true);
        }

        private void ClearLogFields()
        {
            // Clear log fields
            foreach (InputField logField in _logFields)
            {
                Destroy(logField.gameObject);
            }
            _logFields.Clear();
            AddLogField();
        }

        private void RefreshLogFieldsSize()
        {
            // Refresh the width of the log fields to the current width (determined by dev console window width)
            RectTransform rect;
            foreach (InputField logField in _logFields)
            {
                rect = logField.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(_currentLogFieldWidth, rect.sizeDelta.y);
            }
            RebuildLayout();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RebuildLayout()
        {
            // Forcefully rebuild the layout, otherwise transforms are positioned incorrectly
            LayoutRebuilder.ForceRebuildLayoutImmediate(_logContentTransform);
        }

        #endregion

        #region Physical input methods

        private bool GetKeyDown(InputKey key)
        {
#if USE_NEW_INPUT_SYSTEM
            return Keyboard.current[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        private Vector2 GetMousePosition()
        {
#if USE_NEW_INPUT_SYSTEM
            return Mouse.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        #endregion

        #endregion
    }
}