'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2021 IP2Location.com
'---------------------------------------------------------------------------

Imports System.IO

Public Class LogDebug
    Public Shared Sub WriteLog(ByVal mymesg As String)
        Dim myfilename As String = System.AppDomain.CurrentDomain.BaseDirectory & "errLog.txt"
        Dim fs As FileStream = Nothing
        Dim objWriter As StreamWriter = Nothing

        If File.Exists(myfilename) Then
            Try
                fs = New FileStream(myfilename, FileMode.Append, FileAccess.Write, FileShare.Write)
                objWriter = New StreamWriter(fs)
                objWriter.WriteLine(Now & ": " & mymesg)
            Catch ex As Exception
                'just fail silently
            Finally
                If objWriter IsNot Nothing Then
                    objWriter.Close()
                End If
                If fs IsNot Nothing Then
                    fs.Close()
                End If
            End Try
        End If
    End Sub
End Class
