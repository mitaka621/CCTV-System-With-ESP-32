Dim base
base = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
CreateObject("WScript.Shell").Run """" & base & "start-camportal.bat""", 0, True
