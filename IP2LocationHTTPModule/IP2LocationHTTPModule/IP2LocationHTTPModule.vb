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
' Copyright (c) 2002-2020 IP2Location.com
'
'---------------------------------------------------------------------------

Imports System
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Web
Imports System.Xml
Imports System.Xml.Serialization

Public Class HTTPModule : Implements IHttpModule
    Public oIP2Location As IP2Location = Nothing
    Public oIPResult As IPResult = Nothing
    Private oConfig As IP2LocationConfig = Nothing
    Private Const configFile As String = "IP2Location-config.xml"
    Private globalConfig As String = Nothing
    Private baseDir As String = ""
    Public whitespace As Regex
    Private version As String = "8.3" 'follow all the paid components versioning
    'Private Generator As New System.Random(CType(DateTime.Now.Ticks Mod Int32.MaxValue, Integer)) ' testing only

    Public Sub Dispose() Implements System.Web.IHttpModule.Dispose
        LogDebug.WriteLog("Exiting IP2Location HTTP Module")
        If Not oIP2Location Is Nothing Then
            oIP2Location = Nothing
        End If

        If Not oIPResult Is Nothing Then
            oIPResult = Nothing
        End If

        If Not oConfig Is Nothing Then
            oConfig = Nothing
        End If
    End Sub

    Public Sub Init(context As System.Web.HttpApplication) Implements System.Web.IHttpModule.Init
        oIP2Location = New IP2Location
        oIPResult = New IPResult
        Dim mydirectory As String = ""

        globalConfig = Environment.GetEnvironmentVariable("IP2LocationHTTPModuleConfig")
        If Not globalConfig Is Nothing Then 'server level mode
            LogDebug.WriteLog("Global config: " & globalConfig)
            mydirectory = globalConfig
            If Not mydirectory.EndsWith("\") Then
                mydirectory = mydirectory & "\"
            End If
        Else 'website level mode
            baseDir = AppDomain.CurrentDomain.BaseDirectory
            mydirectory = baseDir & "bin\" 'always assume config file in bin folder
        End If

        Try
            LogDebug.WriteLog("Starting IP2Location HTTP Module " & version)
            whitespace = New Regex("\s")

            'CreateConfig(mydirectory & configFile) ' for testing only

            oConfig = ReadConfig(mydirectory & configFile)

            'Here need to input company name and license key for checking registration
            'oIP2Location.Register(oConfig.Settings.CompanyName, oConfig.Settings.LicenseKey)

            'Set BIN file path
            If Not globalConfig Is Nothing Then
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
        Dim myrule As BlockRule = Nothing
        Dim myrule2 As RedirectRule = Nothing
        Dim myIP As String = ""
        Dim myurl As String = request.Url.AbsoluteUri
        Dim bypass As Boolean = False
        Dim bypassip As ByPassIP = Nothing
        ' PRODUCTION ONE
        If oConfig.Settings.CustomIPServerVariable.Trim <> "" Then
            myIP = request.ServerVariables.Item(oConfig.Settings.CustomIPServerVariable.Trim)
        Else
            myIP = request.UserHostAddress
        End If

        ' TESTING ONLY
        'myIP = "8.8.8.8"
        'myIP = "2404:6800:4001:c01::93"
        'myIP = GetIP() ' random IP generator
        'myIP = "2001:0:4136:e378:8000:63bf:f7f7:f7f7"
        'myIP = "2002:0803:2200::0803:2200"

        ' output extra info so we know it is working
        LogDebug.WriteLog("Querying IP: " & myIP)
        oIPResult = oIP2Location.IPQuery(myIP)
        LogDebug.WriteLog("Query Status: " & oIPResult.Status)
        LogDebug.WriteLog("Full URL: " & myurl)

        ' TESTING ONE
        'oIPResult = oIP2Location.IPQuery("72.167.39.152") 'for testing only

        If oIPResult.Status = "OK" OrElse oIPResult.Status = "NOT_REGISTERED" Then
            If Not oConfig.ByPassIPs Is Nothing Then
                For Each bypassip In oConfig.ByPassIPs
                    If bypassip.IP = myIP Then
                        bypass = True
                        Exit For
                    End If
                Next
            End If

            If Not bypass Then
                'blocking rules has priority over redirect rules
                If Not oConfig.BlockRules Is Nothing Then
                    For Each myrule In oConfig.BlockRules
                        If myrule.Enabled Then
                            myrule.Comparison = myrule.Comparison.ToUpper.Trim
                            myrule.Countries = whitespace.Replace(myrule.Countries.ToUpper, String.Empty) 'String.Empty is apparently faster than normal blank string
                            myrule.FromURL = myrule.FromURL.Trim

                            If myrule.Comparison = "IN" Then
                                If myrule.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    'If Regex.IsMatch(request.ServerVariables.Item("SCRIPT_NAME"), myrule.FromURL, RegexOptions.IgnoreCase) Then
                                    If Regex.IsMatch(myurl, myrule.FromURL, RegexOptions.IgnoreCase) Then
                                        app.CompleteRequest()
                                        app.Response.StatusCode = 403
                                        'Throw New HttpException(403, "Forbidden")
                                    End If
                                End If
                            ElseIf myrule.Comparison = "NOT IN" Then
                                If Not myrule.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    'If Regex.IsMatch(request.ServerVariables.Item("SCRIPT_NAME"), myrule.FromURL, RegexOptions.IgnoreCase) Then
                                    If Regex.IsMatch(myurl, myrule.FromURL, RegexOptions.IgnoreCase) Then
                                        app.CompleteRequest()
                                        app.Response.StatusCode = 403
                                        'Throw New HttpException(403, "Forbidden")
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If

                'if above no blocking rules matched, then we can process redirect rules
                If Not oConfig.RedirectRules Is Nothing Then
                    For Each myrule2 In oConfig.RedirectRules
                        If myrule2.Enabled Then
                            myrule2.Comparison = myrule2.Comparison.ToUpper.Trim
                            myrule2.Countries = whitespace.Replace(myrule2.Countries.ToUpper, String.Empty) 'String.Empty is apparently faster than normal blank string
                            myrule2.FromURL = myrule2.FromURL.Trim
                            myrule2.ToURL = myrule2.ToURL.Trim

                            If myrule2.Comparison = "IN" Then
                                If myrule2.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    'If Regex.IsMatch(request.ServerVariables.Item("SCRIPT_NAME"), myrule2.FromURL, RegexOptions.IgnoreCase) Then
                                    If Regex.IsMatch(myurl, myrule2.FromURL, RegexOptions.IgnoreCase) Then
                                        response.Redirect(myrule2.ToURL, True)
                                    End If
                                End If
                            ElseIf myrule2.Comparison = "NOT IN" Then
                                If Not myrule2.Countries.Split(",").Contains(oIPResult.CountryShort) Then
                                    'If Regex.IsMatch(request.ServerVariables.Item("SCRIPT_NAME"), myrule2.FromURL, RegexOptions.IgnoreCase) Then
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

            ' IF NOT REGISTERED, RANDOMLY WILL SHOW EVALUATION MESSAGE IN THE FIELDS
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
            End If
            If oIPResult.Status = "NOT_REGISTERED" Then
                LogDebug.WriteLog("IP2Location HTTP Module running in evaluation mode.")
            End If
        End If
    End Sub

    ''USING THIS FOR TESTING AND GENERATING A CONFIG FILE TEMPLATE
    'Private Sub CreateConfig(ByVal filename As String)
    '    ' Create an instance of the XmlSerializer class; 
    '    ' specify the type of object to serialize. 
    '    Dim serializer As New XmlSerializer(GetType(IP2LocationConfig))
    '    Dim writer As New StreamWriter(filename)
    '    Dim config As New IP2LocationConfig()

    '    Dim mydirectory As String = "bin\"
    '    Dim mybin As String = mydirectory & "IP-COUNTRY-REGION-CITY-LATITUDE-LONGITUDE-ZIPCODE-TIMEZONE-ISP-DOMAIN-NETSPEED-AREACODE-WEATHER-MOBILE-ELEVATION-USAGETYPE-SAMPLE.BIN"
    '    Dim mycompany As String = "Hexasoft Development Sdn Bhd"
    '    Dim mylicense As String = License.GetHash(mycompany)
    '    Dim mycustomipservervariable As String = ""

    '    'SETTINGS OBJECT
    '    Dim mysettings As New Settings
    '    mysettings.BINFile = mybin
    '    mysettings.LicenseKey = mylicense
    '    mysettings.CompanyName = mycompany
    '    mysettings.CustomIPServerVariable = mycustomipservervariable
    '    mysettings.EnabledServerVariable = True

    '    'attach to config
    '    config.Settings = mysettings

    '    'BYPASS HERE
    '    Dim bypassips(0) As ByPassIP

    '    'BYPASS IPs
    '    bypassips(0) = New ByPassIP
    '    bypassips(0).IP = "175.143.9.226"

    '    'attach bypass
    '    config.ByPassIPs = bypassips

    '    'BLOCKING RULES HERE
    '    Dim myrules(1) As BlockRule

    '    'RULE 1
    '    myrules(0) = New BlockRule

    '    myrules(0).Enabled = True
    '    myrules(0).FromURL = ".*\.php"
    '    myrules(0).Comparison = "NOT IN"
    '    myrules(0).Countries = "US,CA,MY"

    '    'RULE 2
    '    myrules(1) = New BlockRule

    '    myrules(1).Enabled = True
    '    myrules(1).FromURL = ".*/Default\.aspx"
    '    myrules(1).Comparison = "IN"
    '    myrules(1).Countries = "MY,SG,AU"

    '    'attach the rules
    '    config.BlockRules = myrules

    '    'testing
    '    'config.BlockRules = Nothing

    '    'REDIRECTION RULES HERE
    '    Dim myrules2(1) As RedirectRule

    '    'RULE 1
    '    myrules2(0) = New RedirectRule

    '    myrules2(0).Enabled = True
    '    myrules2(0).FromURL = ".*\.php"
    '    myrules2(0).ToURL = "http://www.google.com"
    '    myrules2(0).Comparison = "NOT IN"
    '    myrules2(0).Countries = "US,CA,MY"

    '    'RULE 2
    '    myrules2(1) = New RedirectRule

    '    myrules2(1).Enabled = True
    '    myrules2(1).FromURL = ".*/Default\.aspx"
    '    myrules2(1).ToURL = "http://www.google.sg"
    '    myrules2(1).Comparison = "IN"
    '    myrules2(1).Countries = "MY,SG,AU"

    '    'attach the rules
    '    config.RedirectRules = myrules2

    '    'testing
    '    'config.RedirectRules = Nothing

    '    serializer.Serialize(writer, config)
    '    writer.Close()
    'End Sub

    Private Function ReadConfig(ByVal filename As String) As IP2LocationConfig
        ' Create an instance of the XmlSerializer class; 
        ' specify the type of object to be deserialized. 
        Dim serializer As New XmlSerializer(GetType(IP2LocationConfig))
        ' If the XML document has been altered with unknown 
        ' nodes or attributes, handle them with the 
        ' UnknownNode and UnknownAttribute events. 
        AddHandler serializer.UnknownNode, AddressOf serializer_UnknownNode
        AddHandler serializer.UnknownAttribute, AddressOf serializer_UnknownAttribute

        ' A FileStream is needed to read the XML document. 
        'Dim fs As FileStream = Nothing

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

            'fs = New FileStream(filename, FileMode.Open)
            ' Declare an object variable of the type to be deserialized. 
            Dim config As IP2LocationConfig
            ' Use the Deserialize method to restore the object's state with 
            ' data from the XML document.
            'config = CType(serializer.Deserialize(fs), IP2LocationConfig)

            sr2 = New StringReader(line)

            config = CType(serializer.Deserialize(sr2), IP2LocationConfig)

            Return config
        Catch ex As Exception
            'LogDebug.WriteLog(ex.Message & vbNewLine & ex.StackTrace)
            LogDebug.WriteLog(ex.Message)
            Throw 'special case so need to throw here to stop the main process
        Finally
            'If Not fs Is Nothing Then
            '    fs.Close()
            'End If
            If Not sr2 Is Nothing Then
                sr2.Close()
            End If
        End Try
    End Function

    Private Sub serializer_UnknownNode(sender As Object, e As XmlNodeEventArgs)
        LogDebug.WriteLog("Unknown Node:" & e.Name & vbTab & e.Text)
    End Sub 'serializer_UnknownNode


    Private Sub serializer_UnknownAttribute(sender As Object, e As XmlAttributeEventArgs)
        Dim attr As System.Xml.XmlAttribute = e.Attr
        LogDebug.WriteLog("Unknown attribute " & attr.Name & "='" & attr.Value & "'")
    End Sub 'serializer_UnknownAttribute

    ' below 3 functions for testing random IP
    'Public Function GetIP() As String
    '    Dim min As Integer = 0
    '    Dim max As Integer = 255
    '    Dim result As String = ""

    '    If GetRandom(min, max) Mod 2 = 0 Then
    '        result = GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex() & ":" & GetRandomHex()
    '    Else
    '        result = GetRandom(min, max) & "." & GetRandom(min, max) & "." & GetRandom(min, max) & "." & GetRandom(min, max)
    '    End If
    '    Return result
    'End Function

    'Public Function GetRandomHex() As String
    '    Dim x As Integer = 0
    '    Dim hexstr As String = Generator.Next(2363345, 23423425).ToString("X").Substring(0, 4)
    '    Return hexstr
    'End Function

    'Public Function GetRandom(ByVal Min As Integer, ByVal Max As Integer) As Integer
    '    Return Generator.Next(Min, Max)
    'End Function

End Class
