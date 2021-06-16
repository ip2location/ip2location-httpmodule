'--------------------------------------------------------------------------
' Title        : IP2Location HTTP Module
' Description  : This module lookup the IP2Location database from an IP address and does redirections as well as insert server variables.
' Requirements : .NET 3.5 Framework (due to IIS limitations, .NET 3.5 module is the easiest to deploy)
' IIS Versions : 7.0, 7.5, 8.0 & 8.5
'
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2021 IP2Location.com
'
'---------------------------------------------------------------------------

Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Web
Imports System.Xml.Serialization

Public Class HTTPModule : Implements IHttpModule
    Public oIP2Location As IP2Location = Nothing
    Public oIPResult As IPResult = Nothing
    Private oConfig As IP2LocationConfig = Nothing
    Private Const configFile As String = "IP2Location-config.xml"
    Private globalConfig As String = Nothing
    Private baseDir As String = ""
    Public whitespace As Regex
    Private ReadOnly version As String = "8.4" 'follow all the paid components versioning

    Public Sub Dispose() Implements System.Web.IHttpModule.Dispose
        LogDebug.WriteLog("Exiting IP2Location HTTP Module")
        oIP2Location = Nothing
        oIPResult = Nothing
        oConfig = Nothing
    End Sub

    Public Sub Init(context As System.Web.HttpApplication) Implements System.Web.IHttpModule.Init
        oIP2Location = New IP2Location
        oIPResult = New IPResult
        Dim mydirectory As String = ""

        globalConfig = Environment.GetEnvironmentVariable("IP2LocationHTTPModuleConfig")
        If globalConfig IsNot Nothing Then 'server level mode
            LogDebug.WriteLog("Global config: " & globalConfig)
            mydirectory = globalConfig
            If Not mydirectory.EndsWith("\") Then
                mydirectory &= "\"
            End If
        Else 'website level mode
            baseDir = AppDomain.CurrentDomain.BaseDirectory
            mydirectory = baseDir & "bin\" 'always assume config file in bin folder
        End If

        Try
            LogDebug.WriteLog("Starting IP2Location HTTP Module " & version)
            whitespace = New Regex("\s")

            oConfig = ReadConfig(mydirectory & configFile)

            'Set BIN file path
            If globalConfig IsNot Nothing Then
                oIP2Location.IPDatabasePath = oConfig.Settings.BINFile 'global BIN is always full path 
            Else
                oIP2Location.IPDatabasePath = baseDir & oConfig.Settings.BINFile 'website BIN is always relative to website root folder
            End If

            AddHandler context.PreRequestHandlerExecute, AddressOf OnPreExecuteRequestHandler
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message & vbNewLine & ex.StackTrace)
        End Try
    End Sub

    Public Sub OnPreExecuteRequestHandler(sender As Object, e As EventArgs)
        Dim app As HttpApplication = DirectCast(sender, HttpApplication)
        Dim request As HttpRequest = app.Context.Request
        Dim response As HttpResponse = app.Context.Response
        Dim myrule As BlockRule
        Dim myrule2 As RedirectRule
        Dim myIP As String
        Dim myurl As String = request.Url.AbsoluteUri
        Dim bypass As Boolean = False
        Dim bypassip As ByPassIP
        ' PRODUCTION ONE
        If oConfig.Settings.CustomIPServerVariable.Trim <> "" Then
            myIP = request.ServerVariables.Item(oConfig.Settings.CustomIPServerVariable.Trim)
        Else
            myIP = request.UserHostAddress
        End If

        ' output extra info so we know it is working
        LogDebug.WriteLog("Querying IP: " & myIP)
        oIPResult = oIP2Location.IPQuery(myIP)
        LogDebug.WriteLog("Query Status: " & oIPResult.Status)
        LogDebug.WriteLog("Full URL: " & myurl)

        If oIPResult.Status = "OK" OrElse oIPResult.Status = "NOT_REGISTERED" Then
            If oConfig.ByPassIPs IsNot Nothing Then
                For Each bypassip In oConfig.ByPassIPs
                    If bypassip.IP = myIP Then
                        bypass = True
                        Exit For
                    End If
                Next
            End If

            If Not bypass Then
                'blocking rules has priority over redirect rules
                If oConfig.BlockRules IsNot Nothing Then
                    For Each myrule In oConfig.BlockRules
                        If myrule.Enabled Then
                            myrule.Comparison = myrule.Comparison.ToUpper.Trim
                            myrule.Countries = whitespace.Replace(myrule.Countries.ToUpper, String.Empty) 'String.Empty is apparently faster than normal blank string
                            myrule.FromURL = myrule.FromURL.Trim

                            If myrule.Comparison = "IN" Then
                                If myrule.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    If Regex.IsMatch(myurl, myrule.FromURL, RegexOptions.IgnoreCase) Then
                                        app.CompleteRequest()
                                        app.Response.StatusCode = 403
                                    End If
                                End If
                            ElseIf myrule.Comparison = "NOT IN" Then
                                If Not myrule.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    If Regex.IsMatch(myurl, myrule.FromURL, RegexOptions.IgnoreCase) Then
                                        app.CompleteRequest()
                                        app.Response.StatusCode = 403
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If

                'if above no blocking rules matched, then we can process redirect rules
                If oConfig.RedirectRules IsNot Nothing Then
                    For Each myrule2 In oConfig.RedirectRules
                        If myrule2.Enabled Then
                            myrule2.Comparison = myrule2.Comparison.ToUpper.Trim
                            myrule2.Countries = whitespace.Replace(myrule2.Countries.ToUpper, String.Empty) 'String.Empty is apparently faster than normal blank string
                            myrule2.FromURL = myrule2.FromURL.Trim
                            myrule2.ToURL = myrule2.ToURL.Trim

                            If myrule2.Comparison = "IN" Then
                                If myrule2.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    If Regex.IsMatch(myurl, myrule2.FromURL, RegexOptions.IgnoreCase) Then
                                        response.Redirect(myrule2.ToURL, True)
                                    End If
                                End If
                            ElseIf myrule2.Comparison = "NOT IN" Then
                                If Not myrule2.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    If Regex.IsMatch(myurl, myrule2.FromURL, RegexOptions.IgnoreCase) Then
                                        response.Redirect(myrule2.ToURL, True)
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
            Else
                LogDebug.WriteLog("ByPassing IP: " & myIP)
            End If

            If oConfig.Settings.EnabledServerVariable Then
                request.ServerVariables.Item("HTTP_X_COUNTRY_SHORT") = oIPResult.CountryShort
                request.ServerVariables.Item("HTTP_X_COUNTRY_LONG") = oIPResult.CountryLong
                request.ServerVariables.Item("HTTP_X_COUNTRY_REGION") = oIPResult.Region
                request.ServerVariables.Item("HTTP_X_COUNTRY_CITY") = oIPResult.City
                request.ServerVariables.Item("HTTP_X_COUNTRY_ISP") = oIPResult.InternetServiceProvider
                request.ServerVariables.Item("HTTP_X_COUNTRY_LATITUDE") = oIPResult.Latitude
                request.ServerVariables.Item("HTTP_X_COUNTRY_LONGITUDE") = oIPResult.Longitude
                request.ServerVariables.Item("HTTP_X_COUNTRY_DOMAIN") = oIPResult.DomainName
                request.ServerVariables.Item("HTTP_X_COUNTRY_ZIPCODE") = oIPResult.ZipCode
                request.ServerVariables.Item("HTTP_X_COUNTRY_TIMEZONE") = oIPResult.TimeZone
                request.ServerVariables.Item("HTTP_X_COUNTRY_NETSPEED") = oIPResult.NetSpeed
                request.ServerVariables.Item("HTTP_X_COUNTRY_IDD_CODE") = oIPResult.IDDCode
                request.ServerVariables.Item("HTTP_X_COUNTRY_AREA_CODE") = oIPResult.AreaCode
                request.ServerVariables.Item("HTTP_X_COUNTRY_WEATHER_CODE") = oIPResult.WeatherStationCode
                request.ServerVariables.Item("HTTP_X_COUNTRY_WEATHER_NAME") = oIPResult.WeatherStationName
                request.ServerVariables.Item("HTTP_X_COUNTRY_MCC") = oIPResult.MCC
                request.ServerVariables.Item("HTTP_X_COUNTRY_MNC") = oIPResult.MNC
                request.ServerVariables.Item("HTTP_X_COUNTRY_MOBILE_BRAND") = oIPResult.MobileBrand
                request.ServerVariables.Item("HTTP_X_COUNTRY_ELEVATION") = oIPResult.Elevation
                request.ServerVariables.Item("HTTP_X_COUNTRY_USAGE_TYPE") = oIPResult.UsageType
                request.ServerVariables.Item("HTTP_X_COUNTRY_ADDRESS_TYPE") = oIPResult.AddressType
                request.ServerVariables.Item("HTTP_X_COUNTRY_CATEGORY") = oIPResult.Category
            End If
        End If
    End Sub

    Private Function ReadConfig(ByVal filename As String) As IP2LocationConfig
        ' Create an instance of the XmlSerializer class; 
        ' specify the type of object to be deserialized. 
        Dim serializer As New XmlSerializer(GetType(IP2LocationConfig))
        ' If the XML document has been altered with unknown 
        ' nodes or attributes, handle them with the 
        ' UnknownNode and UnknownAttribute events. 
        AddHandler serializer.UnknownNode, AddressOf SerializerUnknownNode
        AddHandler serializer.UnknownAttribute, AddressOf SerializerUnknownAttribute

        ' All these just to make sure XML is following our case sensitivity.
        Dim line As String
        Dim sr2 As StringReader = Nothing
        Dim regexstr As New List(Of String)
        Dim normalelem As String
        normalelem = "Settings|BIN_File|License_Key|Company_Name|Enabled_Server_Variables|ByPassIPs|ByPassIP|IP|BlockRules|BlockRule|URL_Regex|Comparison|Countries|Enabled_Rule|RedirectRules|RedirectRule|Redirect_To_URL"
        Dim elem As String
        Dim elem2 As String

        regexstr.Add("(</?)(IP2Location_Configuration)([>|\s])") 'main element

        For Each elem In normalelem.Split("|")
            regexstr.Add("(</?)(" & elem & ")(>)")
        Next

        Try
            'Have to make sure all the XML supplied by user is correct case
            Using sr As New StreamReader(filename)
                line = sr.ReadToEnd()
            End Using

            'Fix the XML here using replace
            For Each elem In regexstr
                elem2 = elem.Replace(")(", "#").Split("#")(1) 'to get the tag name with specific case sensitivity
                line = Regex.Replace(line, elem, "$1" & elem2 & "$3", RegexOptions.IgnoreCase)
            Next

            ' Declare an object variable of the type to be deserialized. 
            Dim config As IP2LocationConfig
            ' Use the Deserialize method to restore the object's state with 
            ' data from the XML document.

            sr2 = New StringReader(line)

            config = CType(serializer.Deserialize(sr2), IP2LocationConfig)

            Return config
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
            Throw 'special case so need to throw here to stop the main process
        Finally
            If sr2 IsNot Nothing Then
                sr2.Close()
            End If
        End Try
    End Function

    Private Sub SerializerUnknownNode(sender As Object, e As XmlNodeEventArgs)
        LogDebug.WriteLog("Unknown Node:" & e.Name & vbTab & e.Text)
    End Sub

    Private Sub SerializerUnknownAttribute(sender As Object, e As XmlAttributeEventArgs)
        Dim attr As System.Xml.XmlAttribute = e.Attr
        LogDebug.WriteLog("Unknown attribute " & attr.Name & "='" & attr.Value & "'")
    End Sub

End Class
