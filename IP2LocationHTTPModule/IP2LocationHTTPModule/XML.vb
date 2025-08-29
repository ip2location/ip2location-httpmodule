'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2023 IP2Location.com
'---------------------------------------------------------------------------

Imports System.Xml
Imports System.Xml.Serialization

'NOTE: The XMLRootAttribute and XMLElement are for renaming the XML output tags.

<XmlRootAttribute("IP2Location_Configuration")>
Public Class IP2LocationConfig
    Public Settings As Settings

    <XmlArray(IsNullable:=False),
    XmlArrayItem(GetType(ByPassIP), IsNullable:=False)>
    Public ByPassIPs() As ByPassIP

    <XmlArray(IsNullable:=False),
     XmlArrayItem(GetType(BlockRule), IsNullable:=False)>
    Public BlockRules() As BlockRule

    <XmlArray(IsNullable:=False),
     XmlArrayItem(GetType(RedirectRule), IsNullable:=False)>
    Public RedirectRules() As RedirectRule
End Class

Public Class Settings
    <XmlElement("BIN_File")>
    Public BINFile As String

    <XmlElement("Company_Name")>
    Public CompanyName As String

    <XmlElement("License_Key")>
    Public LicenseKey As String

    <XmlElement("Custom_IP_Server_Variable")>
    Public CustomIPServerVariable As String

    <XmlIgnore()>
    Public EnabledServerVariable As Boolean

    ' since we have to accept case-insensitive "true" & "false", no choice but to do this
    <XmlElement("Enabled_Server_Variables")>
    Public Property EnabledServerVariableStr() As String
        Get
            EnabledServerVariableStr = IIf(EnabledServerVariable, "True", "False")
        End Get
        Set(ByVal Value As String)
            EnabledServerVariable = XmlConvert.ToBoolean(Value.ToLower.Trim()) 'only "1" or "0" or "true" or "false" are accepted
        End Set
    End Property
End Class

Public Class ByPassIP
    Public IP As String
End Class

Public Class BlockRule
    <XmlIgnore()>
    Public Enabled As Boolean

    ' since we have to accept case-insensitive "true" & "false", no choice but to do this
    <XmlElement("Enabled_Rule")>
    Public Property EnabledStr() As String
        Get
            EnabledStr = IIf(Enabled, "True", "False")
        End Get
        Set(ByVal Value As String)
            Enabled = XmlConvert.ToBoolean(Value.ToLower.Trim()) 'only "1" or "0" or "true" or "false" are accepted
        End Set
    End Property

    <XmlElement("URL_Regex")>
    Public FromURL As String
    Public Comparison As String
    Public Countries As String
End Class

Public Class RedirectRule

    <XmlIgnore()>
    Public Enabled As Boolean

    ' since we have to accept case-insensitive "true" & "false", no choice but to do this
    <XmlElement("Enabled_Rule")>
    Public Property EnabledStr() As String
        Get
            EnabledStr = IIf(Enabled, "True", "False")
        End Get
        Set(ByVal Value As String)
            Enabled = XmlConvert.ToBoolean(Value.ToLower.Trim()) 'only "1" or "0" or "true" or "false" are accepted
        End Set
    End Property

    <XmlElement("URL_Regex")>
    Public FromURL As String
    <XmlElement("Redirect_To_URL")>
    Public ToURL As String
    Public Comparison As String
    Public Countries As String
End Class
