# In-game Developer Console for Unity
This asset provides an <b>in-game developer console</b> (debug console) for Unity projects, allowing developers or users to execute commands or view incoming Unity messages (i.e. Debug.Log, errors, etc.)

## Setup
Simply import the package into your project and you're good to go. No additional setup is required.
- Import via the Unity package manager.
- Import via the Unity asset store (small cost).
- Download directly from the [releases](https://github.com/DavidF-Dev/Unity-DeveloperConsole/releases) tab & import in Unity (<i>Assets > Import Package</i>).

## Usage
When the game is running, press tilde ``~`` to toggle the dev console window. The window has an input field along the bottom where commands can be entered. Pressing ENTER will execute the typed command.
- Use the UP / DOWN arrows to cycle through the command history.
- Use TAB to cycle through the command names.

### Commands
Commands are in the form: <b>commandName parameter1 parameter2 ... parameterN</b>. Some commands have no parameters!

Typing "<b>commands</b>" will display a list of all available commands in the console log.</br>
Typing "<b>help print</b>" will provide helpful information about the <b>print</b> command.</br>
Typing "<b>print "Hello world!"</b>" will display the message "Hello world!" in the console log.

Text that is encased by quotation marks ``"`` will be interpreted as a single parameter.

### Scripting
The dev console can be accessed via the ``DevConsole`` static class in the ``DavidFDev`` namespace.
- ``Enable/DisableConsole()``: enable or disable the dev console entirely (disabled by default in release builds).
- ``Open/CloseConsole()``: open or close the dev console window.
- ``Log()``: log a message to the dev console.
- ``SetToggleKey()``: change or disable the key used to toggle the dev console window.
- ``AddCommand()``: add a custom command to the dev console database.

#### Example
```cs
using DavidFDev;
DevConsole.EnableConsole();
DevConsole.SetToggleKey(null);
DevConsole.Log("Hello world!");
```

### Custom commands
Custom commands can be added to the dev console by developers. They can be created in two ways:
- Use ``Command.Create()`` to initialise a new command instance, allowing for multiple parameters and aliases.
- Add the ``[DevConsoleCommand]`` attribute above a static method declaration, using the method body as the callback and arguments as command parameters.

#### Parameters
Default supported parameter types implement the ``IConvertible`` interface (e.g. int, float, string, bool, etc.)</br>
Enums are also supported.</br>
To add a custom type, use ``DevConsole.AddParameterType<T>()`` (see FAQ below).

#### Example using Command.Create
```cs
DevConsole.AddCommand(Command.Create<string>(
  name: "print",
  aliases: "say,display",
  helpText: "Display a message in the dev console",
  p1: Parameter.Create(
    name: "message",
    helpText: "Message to display"
  ),
  callback: (string message) => DevConsole.Log(message)
));
```

#### Example using attribute
```cs
[DevConsoleCommand(
  name: "print",
  aliases: "say,display",
  helpText: "Display a message in the dev console",
  parameterHelpText: "Message to display"
)]
private static void Print(string message) => DevConsole.Log(message);
```

### Built-in commands
The asset provides various built-in commands.
- ``commands``: display a list of available commands.
- ``help (String)commandName``: display helpful information about the specified command.
- ``print (String)message``: display a message in the dev console log.
- ``exit``: exit the game.
- ``fullscreen (Boolean)enabled``: query or set whether the window is fullscreen.
- ``scene_load (Int32)buildIndex``: load a scene.
- ``scene_info (Int32)sceneIndex``: display information about an active scene.

And more...

## FAQ
<b>Q. Does the asset support the new input system?</b></br>
A. Yes, the dev console supports both the legacy and new input system. The correct input system will be chosen automatically by the asset during compilation.

<b>Q. Does the dev console work in release builds?</b></br>
A. Yes, the dev console can be used in release builds, but will need to be enabled via script: ``DevConsole.EnableConsole()``. It can be included in commercial projects as long as the [licensing conditions](https://github.com/DavidF-Dev/Unity-DeveloperConsole/blob/main/LICENSE.md) are fulfilled. Furthermore, specific commands can be set up to only work in development builds.

<b>Q. Why does the Unity asset store version cost money?</b></br>
A. For developers that would like the asset linked to their Unity account, or just want to show me a little support. Paid owners of the asset will also receive prioritised support.

<b>Q. How do I add a custom parameter type?</b></br>
A. Use ``DevConsole.AddParameterType<T>()`` to enable the use of the specified type in commands. A parser function must be provided, which converts a string into the parameter type.
```cs
DevConsole.AddParameterType<GameObject>((string input) => GameObject.Find(input));
```

<b>Q. Can I remove a built-in command?</b></br>
A. Yes, use ``DevConsole.RemoveCommand()`` to remove almost any command. There are 4 permanent commands that cannot be removed (``devconsole``, ``commmands``, ``help`` and ``print``).

## Acknowledgements
- [yasirkula's Unity In-game Debug Console](https://github.com/yasirkula/UnityIngameDebugConsole) for providing inspiration for parts of my asset.
- [@exdli](https://twitter.com/exdli) for help with supporting both input systems.
- [FiraCode](https://github.com/tonsky/FiraCode) font used under the SIL Open Font License 1.1.
