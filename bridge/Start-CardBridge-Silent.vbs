Option Explicit

Dim shell, fileSystem, baseDirectory, appDirectory, executable, command
Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

baseDirectory = fileSystem.GetParentFolderName(WScript.ScriptFullName)
appDirectory = fileSystem.BuildPath(baseDirectory, "app")
executable = fileSystem.BuildPath(appDirectory, "CardBridge.exe")

If Not fileSystem.FileExists(executable) Then
    WScript.Quit 1
End If

command = Chr(34) & executable & Chr(34) & " --allow-carcheck-web --no-browser"
shell.CurrentDirectory = appDirectory
shell.Run command, 0, False
