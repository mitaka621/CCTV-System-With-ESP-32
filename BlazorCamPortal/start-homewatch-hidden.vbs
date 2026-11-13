Dim base
base = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
CreateObject("WScript.Shell").Run """" & base & "start-homewatch.bat""", 0, True
