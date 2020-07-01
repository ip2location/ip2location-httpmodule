'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2020 IP2Location.com
'
' NOTE: Due IIS 7/7.5/8.0/8.5 being able to easily use .NET 3.5 managed module, this component also has been modified to use .NET 3.5
'       and for IPv6 calculations we IntXLib since .NET 3.5 does not come with BigInteger class.
'---------------------------------------------------------------------------
Imports System
Imports System.IO
Imports System.Data
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.DateTime
Imports System.Text.RegularExpressions
Imports System.Linq
Imports IntXLib ' installed via NuGet

Public NotInheritable Class IP2Location
    Private _DBFilePath As String = ""
    Private _HasKey As Boolean = True ' license no longer required
    Private _MetaData As MetaData = Nothing
    Private _OutlierCase1 As Regex = New Regex("^:(:[\dA-F]{1,4}){7}$", RegexOptions.IgnoreCase)
    Private _OutlierCase2 As Regex = New Regex("^:(:[\dA-F]{1,4}){5}:(\d{1,3}\.){3}\d{1,3}$", RegexOptions.IgnoreCase)
    Private _OutlierCase3 As Regex = New Regex("^\d+$")
    Private _OutlierCase4 As Regex = New Regex("^([\dA-F]{1,4}:){6}(0\d+\.|.*?\.0\d+).*$")
    Private _IPv4MappedRegex As Regex = New Regex("^(.*:)((\d+\.){3}\d+)$")
    Private _IPv4MappedRegex2 As Regex = New Regex("^.*((:[\dA-F]{1,4}){2})$")
    Private _IPv4CompatibleRegex As Regex = New Regex("^::[\dA-F]{1,4}$", RegexOptions.IgnoreCase)
    Private _IPv4ColumnSize As Integer = 0
    Private _IPv6ColumnSize As Integer = 0
    Private _IndexArrayIPv4(65535, 1) As Integer
    Private _IndexArrayIPv6(65535, 1) As Integer
    'Private _IndexShiftIPv4 As Integer = Math.Pow(2, 16)
    'Private _IndexShiftIPv6 As Integer = Math.Pow(2, 112)
    'Private _IndexShiftIPv4 As New IntX(Math.Pow(2, 16).ToString)
    'Private _IndexShiftIPv6 As New IntX(Math.Pow(2, 112).ToString)

    'Private _fromBI As BigInt = New BigInt("281470681743360")
    'Private _toBI As BigInt = New BigInt("281474976710655")
    Private _fromBI As New IntX("281470681743360")
    Private _toBI As New IntX("281474976710655")
    Private _FromBI2 As New IntX("42545680458834377588178886921629466624")
    Private _ToBI2 As New IntX("42550872755692912415807417417958686719")
    Private _FromBI3 As New IntX("42540488161975842760550356425300246528")
    Private _ToBI3 As New IntX("42540488241204005274814694018844196863")
    Private _DivBI As New IntX("4294967295")

    Private Const FIVESEGMENTS As String = "0000:0000:0000:0000:0000:"
    'Private SHIFT64BIT As BigInt = New BigInt("18446744073709551616")
    'Private MAX_IPV4_RANGE As BigInt = New BigInt("4294967295")
    'Private MAX_IPV6_RANGE As BigInt = New BigInt("340282366920938463463374607431768211455")
    Private SHIFT64BIT As New IntX("18446744073709551616")
    Private MAX_IPV4_RANGE As New IntX("4294967295")
    Private MAX_IPV6_RANGE As New IntX("340282366920938463463374607431768211455")
    Private Const MSG_OK As String = "OK"
    Private Const MSG_NOT_SUPPORTED As String = "This method is not applicable for current IP2Location binary data file. Please upgrade your subscription package to install new data file."
    Private Const MSG_EVALUATION As String = "EVALUATION"
    Private Const MSG_NOT_REGISTERED As String = "NOT_REGISTERED"
 
    Private COUNTRY_POSITION() As Byte = {0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2}
    Private REGION_POSITION() As Byte = {0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3}
    Private CITY_POSITION() As Byte = {0, 0, 0, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4}
    Private ISP_POSITION() As Byte = {0, 0, 3, 0, 5, 0, 7, 5, 7, 0, 8, 0, 9, 0, 9, 0, 9, 0, 9, 7, 9, 0, 9, 7, 9}
    Private LATITUDE_POSITION() As Byte = {0, 0, 0, 0, 0, 5, 5, 0, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5}
    Private LONGITUDE_POSITION() As Byte = {0, 0, 0, 0, 0, 6, 6, 0, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6}
    Private DOMAIN_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 6, 8, 0, 9, 0, 10, 0, 10, 0, 10, 0, 10, 8, 10, 0, 10, 8, 10}
    Private ZIPCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 7, 7, 7, 0, 7, 7, 7, 0, 7, 0, 7, 7, 7, 0, 7}
    Private TIMEZONE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 8, 7, 8, 8, 8, 7, 8, 0, 8, 8, 8, 0, 8}
    Private NETSPEED_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 11, 0, 11, 8, 11, 0, 11, 0, 11, 0, 11}
    Private IDDCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 12, 0, 12, 0, 12, 9, 12, 0, 12}
    Private AREACODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 13, 0, 13, 0, 13, 10, 13, 0, 13}
    Private WEATHERSTATIONCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 14, 0, 14, 0, 14, 0, 14}
    Private WEATHERSTATIONNAME_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 15, 0, 15, 0, 15, 0, 15}
    Private MCC_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 16, 0, 16, 9, 16}
    Private MNC_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 17, 0, 17, 10, 17}
    Private MOBILEBRAND_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 18, 0, 18, 11, 18}
    Private ELEVATION_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 19, 0, 19}
    Private USAGETYPE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 20}

    Private COUNTRY_POSITION_OFFSET As Integer = 0
    Private REGION_POSITION_OFFSET As Integer = 0
    Private CITY_POSITION_OFFSET As Integer = 0
    Private ISP_POSITION_OFFSET As Integer = 0
    Private DOMAIN_POSITION_OFFSET As Integer = 0
    Private ZIPCODE_POSITION_OFFSET As Integer = 0
    Private LATITUDE_POSITION_OFFSET As Integer = 0
    Private LONGITUDE_POSITION_OFFSET As Integer = 0
    Private TIMEZONE_POSITION_OFFSET As Integer = 0
    Private NETSPEED_POSITION_OFFSET As Integer = 0
    Private IDDCODE_POSITION_OFFSET As Integer = 0
    Private AREACODE_POSITION_OFFSET As Integer = 0
    Private WEATHERSTATIONCODE_POSITION_OFFSET As Integer = 0
    Private WEATHERSTATIONNAME_POSITION_OFFSET As Integer = 0
    Private MCC_POSITION_OFFSET As Integer = 0
    Private MNC_POSITION_OFFSET As Integer = 0
    Private MOBILEBRAND_POSITION_OFFSET As Integer = 0
    Private ELEVATION_POSITION_OFFSET As Integer = 0
    Private USAGETYPE_POSITION_OFFSET As Integer = 0

    Private COUNTRY_ENABLED As Boolean = False
    Private REGION_ENABLED As Boolean = False
    Private CITY_ENABLED As Boolean = False
    Private ISP_ENABLED As Boolean = False
    Private DOMAIN_ENABLED As Boolean = False
    Private ZIPCODE_ENABLED As Boolean = False
    Private LATITUDE_ENABLED As Boolean = False
    Private LONGITUDE_ENABLED As Boolean = False
    Private TIMEZONE_ENABLED As Boolean = False
    Private NETSPEED_ENABLED As Boolean = False
    Private IDDCODE_ENABLED As Boolean = False
    Private AREACODE_ENABLED As Boolean = False
    Private WEATHERSTATIONCODE_ENABLED As Boolean = False
    Private WEATHERSTATIONNAME_ENABLED As Boolean = False
    Private MCC_ENABLED As Boolean = False
    Private MNC_ENABLED As Boolean = False
    Private MOBILEBRAND_ENABLED As Boolean = False
    Private ELEVATION_ENABLED As Boolean = False
    Private USAGETYPE_ENABLED As Boolean = False

    ' Description: Set/Get the value of IPv4 database path
    Public Property IPDatabasePath() As String
        Get
            Return _DBFilePath
        End Get
        Set(ByVal Value As String)
            _DBFilePath = Value
        End Set
    End Property

    ' Description: Make sure the component is registered (DEPRECATED)
    Public Function IsRegistered() As Boolean
        Return _HasKey
    End Function

    ' Description: Read BIN file
    Private Function LoadBIN() As Boolean
        Dim loadOK As Boolean = False
        Try
            If _DBFilePath <> "" Then
                Using myFileStream As FileStream = New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read)
                    _MetaData = New MetaData
                    With _MetaData
                        .DBType = read8(1, myFileStream)
                        .DBColumn = read8(2, myFileStream)
                        .DBYear = read8(3, myFileStream)
                        .DBMonth = read8(4, myFileStream)
                        .DBDay = read8(5, myFileStream)
                        .DBCount = read32(6, myFileStream) '4 bytes
                        .BaseAddr = read32(10, myFileStream) '4 bytes
                        .DBCountIPv6 = read32(14, myFileStream) '4 bytes
                        .BaseAddrIPv6 = read32(18, myFileStream) '4 bytes
                        .IndexBaseAddr = read32(22, myFileStream) '4 bytes
                        .IndexBaseAddrIPv6 = read32(26, myFileStream) '4 bytes

                        If .IndexBaseAddr > 0 Then
                            .Indexed = True
                        End If

                        If .DBCountIPv6 = 0 Then ' old style IPv4-only BIN file
                            .OldBIN = True
                        Else
                            If .IndexBaseAddrIPv6 > 0 Then
                                .IndexedIPv6 = True
                            End If
                        End If

                        _IPv4ColumnSize = .DBColumn << 2 ' 4 bytes each column
                        _IPv6ColumnSize = 16 + ((.DBColumn - 1) << 2) ' 4 bytes each column, except IPFrom column which is 16 bytes

                        Dim dbt As Integer = .DBType

                        ' since both IPv4 and IPv6 use 4 bytes for the below columns, can just do it once here
                        'COUNTRY_POSITION_OFFSET = If(COUNTRY_POSITION(dbt) <> 0, (COUNTRY_POSITION(dbt) - 1) << 2, 0)
                        'REGION_POSITION_OFFSET = If(REGION_POSITION(dbt) <> 0, (REGION_POSITION(dbt) - 1) << 2, 0)
                        'CITY_POSITION_OFFSET = If(CITY_POSITION(dbt) <> 0, (CITY_POSITION(dbt) - 1) << 2, 0)
                        'ISP_POSITION_OFFSET = If(ISP_POSITION(dbt) <> 0, (ISP_POSITION(dbt) - 1) << 2, 0)
                        'DOMAIN_POSITION_OFFSET = If(DOMAIN_POSITION(dbt) <> 0, (DOMAIN_POSITION(dbt) - 1) << 2, 0)
                        'ZIPCODE_POSITION_OFFSET = If(ZIPCODE_POSITION(dbt) <> 0, (ZIPCODE_POSITION(dbt) - 1) << 2, 0)
                        'LATITUDE_POSITION_OFFSET = If(LATITUDE_POSITION(dbt) <> 0, (LATITUDE_POSITION(dbt) - 1) << 2, 0)
                        'LONGITUDE_POSITION_OFFSET = If(LONGITUDE_POSITION(dbt) <> 0, (LONGITUDE_POSITION(dbt) - 1) << 2, 0)
                        'TIMEZONE_POSITION_OFFSET = If(TIMEZONE_POSITION(dbt) <> 0, (TIMEZONE_POSITION(dbt) - 1) << 2, 0)
                        'NETSPEED_POSITION_OFFSET = If(NETSPEED_POSITION(dbt) <> 0, (NETSPEED_POSITION(dbt) - 1) << 2, 0)
                        'IDDCODE_POSITION_OFFSET = If(IDDCODE_POSITION(dbt) <> 0, (IDDCODE_POSITION(dbt) - 1) << 2, 0)
                        'AREACODE_POSITION_OFFSET = If(AREACODE_POSITION(dbt) <> 0, (AREACODE_POSITION(dbt) - 1) << 2, 0)
                        'WEATHERSTATIONCODE_POSITION_OFFSET = If(WEATHERSTATIONCODE_POSITION(dbt) <> 0, (WEATHERSTATIONCODE_POSITION(dbt) - 1) << 2, 0)
                        'WEATHERSTATIONNAME_POSITION_OFFSET = If(WEATHERSTATIONNAME_POSITION(dbt) <> 0, (WEATHERSTATIONNAME_POSITION(dbt) - 1) << 2, 0)
                        'MCC_POSITION_OFFSET = If(MCC_POSITION(dbt) <> 0, (MCC_POSITION(dbt) - 1) << 2, 0)
                        'MNC_POSITION_OFFSET = If(MNC_POSITION(dbt) <> 0, (MNC_POSITION(dbt) - 1) << 2, 0)
                        'MOBILEBRAND_POSITION_OFFSET = If(MOBILEBRAND_POSITION(dbt) <> 0, (MOBILEBRAND_POSITION(dbt) - 1) << 2, 0)
                        'ELEVATION_POSITION_OFFSET = If(ELEVATION_POSITION(dbt) <> 0, (ELEVATION_POSITION(dbt) - 1) << 2, 0)
                        'USAGETYPE_POSITION_OFFSET = If(USAGETYPE_POSITION(dbt) <> 0, (USAGETYPE_POSITION(dbt) - 1) << 2, 0)

                        ' slightly different offset for reading by row
                        COUNTRY_POSITION_OFFSET = If(COUNTRY_POSITION(dbt) <> 0, (COUNTRY_POSITION(dbt) - 2) << 2, 0)
                        REGION_POSITION_OFFSET = If(REGION_POSITION(dbt) <> 0, (REGION_POSITION(dbt) - 2) << 2, 0)
                        CITY_POSITION_OFFSET = If(CITY_POSITION(dbt) <> 0, (CITY_POSITION(dbt) - 2) << 2, 0)
                        ISP_POSITION_OFFSET = If(ISP_POSITION(dbt) <> 0, (ISP_POSITION(dbt) - 2) << 2, 0)
                        DOMAIN_POSITION_OFFSET = If(DOMAIN_POSITION(dbt) <> 0, (DOMAIN_POSITION(dbt) - 2) << 2, 0)
                        ZIPCODE_POSITION_OFFSET = If(ZIPCODE_POSITION(dbt) <> 0, (ZIPCODE_POSITION(dbt) - 2) << 2, 0)
                        LATITUDE_POSITION_OFFSET = If(LATITUDE_POSITION(dbt) <> 0, (LATITUDE_POSITION(dbt) - 2) << 2, 0)
                        LONGITUDE_POSITION_OFFSET = If(LONGITUDE_POSITION(dbt) <> 0, (LONGITUDE_POSITION(dbt) - 2) << 2, 0)
                        TIMEZONE_POSITION_OFFSET = If(TIMEZONE_POSITION(dbt) <> 0, (TIMEZONE_POSITION(dbt) - 2) << 2, 0)
                        NETSPEED_POSITION_OFFSET = If(NETSPEED_POSITION(dbt) <> 0, (NETSPEED_POSITION(dbt) - 2) << 2, 0)
                        IDDCODE_POSITION_OFFSET = If(IDDCODE_POSITION(dbt) <> 0, (IDDCODE_POSITION(dbt) - 2) << 2, 0)
                        AREACODE_POSITION_OFFSET = If(AREACODE_POSITION(dbt) <> 0, (AREACODE_POSITION(dbt) - 2) << 2, 0)
                        WEATHERSTATIONCODE_POSITION_OFFSET = If(WEATHERSTATIONCODE_POSITION(dbt) <> 0, (WEATHERSTATIONCODE_POSITION(dbt) - 2) << 2, 0)
                        WEATHERSTATIONNAME_POSITION_OFFSET = If(WEATHERSTATIONNAME_POSITION(dbt) <> 0, (WEATHERSTATIONNAME_POSITION(dbt) - 2) << 2, 0)
                        MCC_POSITION_OFFSET = If(MCC_POSITION(dbt) <> 0, (MCC_POSITION(dbt) - 2) << 2, 0)
                        MNC_POSITION_OFFSET = If(MNC_POSITION(dbt) <> 0, (MNC_POSITION(dbt) - 2) << 2, 0)
                        MOBILEBRAND_POSITION_OFFSET = If(MOBILEBRAND_POSITION(dbt) <> 0, (MOBILEBRAND_POSITION(dbt) - 2) << 2, 0)
                        ELEVATION_POSITION_OFFSET = If(ELEVATION_POSITION(dbt) <> 0, (ELEVATION_POSITION(dbt) - 2) << 2, 0)
                        USAGETYPE_POSITION_OFFSET = If(USAGETYPE_POSITION(dbt) <> 0, (USAGETYPE_POSITION(dbt) - 2) << 2, 0)

                        COUNTRY_ENABLED = If(COUNTRY_POSITION(dbt) <> 0, True, False)
                        REGION_ENABLED = If(REGION_POSITION(dbt) <> 0, True, False)
                        CITY_ENABLED = If(CITY_POSITION(dbt) <> 0, True, False)
                        ISP_ENABLED = If(ISP_POSITION(dbt) <> 0, True, False)
                        LATITUDE_ENABLED = If(LATITUDE_POSITION(dbt) <> 0, True, False)
                        LONGITUDE_ENABLED = If(LONGITUDE_POSITION(dbt) <> 0, True, False)
                        DOMAIN_ENABLED = If(DOMAIN_POSITION(dbt) <> 0, True, False)
                        ZIPCODE_ENABLED = If(ZIPCODE_POSITION(dbt) <> 0, True, False)
                        TIMEZONE_ENABLED = If(TIMEZONE_POSITION(dbt) <> 0, True, False)
                        NETSPEED_ENABLED = If(NETSPEED_POSITION(dbt) <> 0, True, False)
                        IDDCODE_ENABLED = If(IDDCODE_POSITION(dbt) <> 0, True, False)
                        AREACODE_ENABLED = If(AREACODE_POSITION(dbt) <> 0, True, False)
                        WEATHERSTATIONCODE_ENABLED = If(WEATHERSTATIONCODE_POSITION(dbt) <> 0, True, False)
                        WEATHERSTATIONNAME_ENABLED = If(WEATHERSTATIONNAME_POSITION(dbt) <> 0, True, False)
                        MCC_ENABLED = If(MCC_POSITION(dbt) <> 0, True, False)
                        MNC_ENABLED = If(MNC_POSITION(dbt) <> 0, True, False)
                        MOBILEBRAND_ENABLED = If(MOBILEBRAND_POSITION(dbt) <> 0, True, False)
                        ELEVATION_ENABLED = If(ELEVATION_POSITION(dbt) <> 0, True, False)
                        USAGETYPE_ENABLED = If(USAGETYPE_POSITION(dbt) <> 0, True, False)

                        If .Indexed Then
                            Dim pointer As Integer = .IndexBaseAddr

                            ' read IPv4 index
                            For x As Integer = _IndexArrayIPv4.GetLowerBound(0) To _IndexArrayIPv4.GetUpperBound(0)
                                _IndexArrayIPv4(x, 0) = read32(pointer, myFileStream) '4 bytes for from row
                                _IndexArrayIPv4(x, 1) = read32(pointer + 4, myFileStream) '4 bytes for to row
                                pointer += 8
                            Next

                            If .IndexedIPv6 Then
                                ' read IPv6 index
                                For x As Integer = _IndexArrayIPv6.GetLowerBound(0) To _IndexArrayIPv6.GetUpperBound(0)
                                    _IndexArrayIPv6(x, 0) = read32(pointer, myFileStream) '4 bytes for from row
                                    _IndexArrayIPv6(x, 1) = read32(pointer + 4, myFileStream) '4 bytes for to row
                                    pointer += 8
                                Next
                            End If
                        End If

                    End With
                End Using
                loadOK = True
            End If
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
        End Try

        Return loadOK
    End Function

    ' Description: Reverse the bytes if system is little endian
    Public Sub LittleEndian(ByRef byteArr() As Byte)
        If System.BitConverter.IsLittleEndian Then
            Dim byteList As New System.Collections.Generic.List(Of Byte)(byteArr)
            byteList.Reverse()
            byteArr = byteList.ToArray()
        End If
    End Sub

    ' Description: Query database to get location information by IP address
    Public Function IPQuery(ByVal myIPAddress As String) As IPResult
        Dim obj As New IPResult
        Dim strIP As String
        Dim myIPType As Integer = 0
        Dim myDBType As Integer = 0
        Dim myBaseAddr As Integer = 0
        Dim myDBColumn As Integer = 0
        Dim myFilestream As FileStream = Nothing

        Dim countrypos As Long = 0
        Dim low As Long = 0
        Dim high As Long = 0
        Dim mid As Long = 0
        'Dim ipfrom As BigInt = New BigInt("0")
        'Dim ipto As BigInt = New BigInt("0")
        'Dim ipnum As BigInt = New BigInt("0")
        'Dim MAX_IP_RANGE As BigInt = New BigInt("0")
        Dim ipfrom As New IntX()
        Dim ipto As New IntX()
        Dim ipnum As New IntX()
        Dim indexaddr As Long = 0
        Dim MAX_IP_RANGE As New IntX()
        Dim rowoffset As Long = 0
        Dim rowoffset2 As Long = 0
        Dim myColumnSize As Integer = 0

        Try
            If myIPAddress = "" OrElse myIPAddress Is Nothing Then
                obj.Status = "EMPTY_IP_ADDRESS"
                Return obj
                Exit Try
            End If

            strIP = Me.VerifyIP(myIPAddress, myIPType, ipnum)
            If strIP <> "Invalid IP" Then
                myIPAddress = strIP
            Else
                obj.Status = "INVALID_IP_ADDRESS"
                Return obj
                'End Try
            End If

            ' Read BIN if haven't done so
            If _MetaData Is Nothing Then
                If Not LoadBIN() Then ' problems reading BIN
                    obj.Status = "MISSING_FILE"
                    Return obj
                    Exit Try
                End If
            End If

            myDBType = _MetaData.DBType
            myDBColumn = _MetaData.DBColumn

            myFilestream = New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)

            Select Case myIPType
                Case 4
                    ' IPv4
                    MAX_IP_RANGE = MAX_IPV4_RANGE
                    high = _MetaData.DBCount
                    myBaseAddr = _MetaData.BaseAddr
                    myColumnSize = _IPv4ColumnSize

                    If _MetaData.Indexed Then
                        indexaddr = ipnum >> 16 'new style for array

                        low = _IndexArrayIPv4(indexaddr, 0)
                        high = _IndexArrayIPv4(indexaddr, 1)
                    End If
                Case 6
                    ' IPv6
                    If _MetaData.OldBIN Then ' old IPv4-only BIN don't contain IPv6 data
                        obj.Status = "IPV6_NOT_SUPPORTED"
                        Return obj
                        Exit Try
                    End If
                    MAX_IP_RANGE = MAX_IPV6_RANGE
                    high = _MetaData.DBCountIPv6
                    myBaseAddr = _MetaData.BaseAddrIPv6
                    myColumnSize = _IPv6ColumnSize

                    If _MetaData.IndexedIPv6 Then
                        indexaddr = ipnum >> 112 'new style for array

                        low = _IndexArrayIPv6(indexaddr, 0)
                        high = _IndexArrayIPv6(indexaddr, 1)
                    End If
            End Select

            If ipnum >= MAX_IP_RANGE Then
                'ipnum = MAX_IP_RANGE - New BigInt("1")
                ipnum = MAX_IP_RANGE - New IntX(1)
            End If

            While (low <= high)
                mid = CInt((low + high) / 2)

                rowoffset = myBaseAddr + (mid * myColumnSize)
                rowoffset2 = rowoffset + myColumnSize

                ipfrom = read32or128(rowoffset, myIPType, myFilestream)
                ipto = read32or128(rowoffset2, myIPType, myFilestream)

                If ipnum >= ipfrom AndAlso ipnum < ipto Then
                    Dim country_short As String = MSG_NOT_SUPPORTED
                    Dim country_long As String = MSG_NOT_SUPPORTED
                    Dim region As String = MSG_NOT_SUPPORTED
                    Dim city As String = MSG_NOT_SUPPORTED
                    Dim isp As String = MSG_NOT_SUPPORTED
                    Dim latitude As Single = 0.0
                    Dim longitude As Single = 0.0
                    Dim domain As String = MSG_NOT_SUPPORTED
                    Dim zipcode As String = MSG_NOT_SUPPORTED
                    Dim timezone As String = MSG_NOT_SUPPORTED
                    Dim netspeed As String = MSG_NOT_SUPPORTED
                    Dim iddcode As String = MSG_NOT_SUPPORTED
                    Dim areacode As String = MSG_NOT_SUPPORTED
                    Dim weatherstationcode As String = MSG_NOT_SUPPORTED
                    Dim weatherstationname As String = MSG_NOT_SUPPORTED
                    Dim mcc As String = MSG_NOT_SUPPORTED
                    Dim mnc As String = MSG_NOT_SUPPORTED
                    Dim mobilebrand As String = MSG_NOT_SUPPORTED
                    Dim elevation As Single = 0.0
                    Dim usagetype As String = MSG_NOT_SUPPORTED

                    Dim firstCol As Integer = 4 ' for IPv4, IP From is 4 bytes
                    If myIPType = 6 Then ' IPv6
                        firstCol = 16 ' 16 bytes for IPv6
                        'rowoffset = rowoffset + 12 ' coz below is assuming all columns are 4 bytes, so got 12 left to go to make 16 bytes total
                    End If

                    ' read the row here after the IP From column (remaining columns are all 4 bytes)
                    Dim row() As Byte = readrow(rowoffset + firstCol, myColumnSize - firstCol, myFilestream)

                    If COUNTRY_ENABLED Then
                        'countrypos = read32(rowoffset + COUNTRY_POSITION_OFFSET, myFilestream)
                        countrypos = read32_row(row, COUNTRY_POSITION_OFFSET)
                        country_short = readStr(countrypos, myFilestream)
                        country_long = readStr(countrypos + 3, myFilestream)
                    End If
                    If REGION_ENABLED Then
                        'region = readStr(read32(rowoffset + REGION_POSITION_OFFSET, myFilestream), myFilestream)
                        region = readStr(read32_row(row, REGION_POSITION_OFFSET), myFilestream)
                    End If
                    If CITY_ENABLED Then
                        'city = readStr(read32(rowoffset + CITY_POSITION_OFFSET, myFilestream), myFilestream)
                        city = readStr(read32_row(row, CITY_POSITION_OFFSET), myFilestream)
                    End If
                    If ISP_ENABLED Then
                        'isp = readStr(read32(rowoffset + ISP_POSITION_OFFSET, myFilestream), myFilestream)
                        isp = readStr(read32_row(row, ISP_POSITION_OFFSET), myFilestream)
                    End If
                    If DOMAIN_ENABLED Then
                        'domain = readStr(read32(rowoffset + DOMAIN_POSITION_OFFSET, myFilestream), myFilestream)
                        domain = readStr(read32_row(row, DOMAIN_POSITION_OFFSET), myFilestream)
                    End If
                    If ZIPCODE_ENABLED Then
                        'zipcode = readStr(read32(rowoffset + ZIPCODE_POSITION_OFFSET, myFilestream), myFilestream)
                        zipcode = readStr(read32_row(row, ZIPCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If LATITUDE_ENABLED Then
                        'latitude = readFloat(rowoffset + LATITUDE_POSITION_OFFSET, myFilestream)
                        latitude = readFloat_row(row, LATITUDE_POSITION_OFFSET)
                    End If
                    If LONGITUDE_ENABLED Then
                        'longitude = readFloat(rowoffset + LONGITUDE_POSITION_OFFSET, myFilestream)
                        longitude = readFloat_row(row, LONGITUDE_POSITION_OFFSET)
                    End If
                    If TIMEZONE_ENABLED Then
                        'timezone = readStr(read32(rowoffset + TIMEZONE_POSITION_OFFSET, myFilestream), myFilestream)
                        timezone = readStr(read32_row(row, TIMEZONE_POSITION_OFFSET), myFilestream)
                    End If
                    If NETSPEED_ENABLED Then
                        'netspeed = readStr(read32(rowoffset + NETSPEED_POSITION_OFFSET, myFilestream), myFilestream)
                        netspeed = readStr(read32_row(row, NETSPEED_POSITION_OFFSET), myFilestream)
                    End If
                    If IDDCODE_ENABLED Then
                        'iddcode = readStr(read32(rowoffset + IDDCODE_POSITION_OFFSET, myFilestream), myFilestream)
                        iddcode = readStr(read32_row(row, IDDCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If AREACODE_ENABLED Then
                        'areacode = readStr(read32(rowoffset + AREACODE_POSITION_OFFSET, myFilestream), myFilestream)
                        areacode = readStr(read32_row(row, AREACODE_POSITION_OFFSET), myFilestream)
                    End If
                    If WEATHERSTATIONCODE_ENABLED Then
                        'weatherstationcode = readStr(read32(rowoffset + WEATHERSTATIONCODE_POSITION_OFFSET, myFilestream), myFilestream)
                        weatherstationcode = readStr(read32_row(row, WEATHERSTATIONCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If WEATHERSTATIONNAME_ENABLED Then
                        'weatherstationname = readStr(read32(rowoffset + WEATHERSTATIONNAME_POSITION_OFFSET, myFilestream), myFilestream)
                        weatherstationname = readStr(read32_row(row, WEATHERSTATIONNAME_POSITION_OFFSET), myFilestream)
                    End If
                    If MCC_ENABLED Then
                        'mcc = readStr(read32(rowoffset + MCC_POSITION_OFFSET, myFilestream), myFilestream)
                        mcc = readStr(read32_row(row, MCC_POSITION_OFFSET), myFilestream)
                    End If
                    If MNC_ENABLED Then
                        'mnc = readStr(read32(rowoffset + MNC_POSITION_OFFSET, myFilestream), myFilestream)
                        mnc = readStr(read32_row(row, MNC_POSITION_OFFSET), myFilestream)
                    End If
                    If MOBILEBRAND_ENABLED Then
                        'mobilebrand = readStr(read32(rowoffset + MOBILEBRAND_POSITION_OFFSET, myFilestream), myFilestream)
                        mobilebrand = readStr(read32_row(row, MOBILEBRAND_POSITION_OFFSET), myFilestream)
                    End If
                    If ELEVATION_ENABLED Then
                        'Single.TryParse(readStr(read32(rowoffset + ELEVATION_POSITION_OFFSET, myFilestream), myFilestream), elevation)
                        Single.TryParse(readStr(read32_row(row, ELEVATION_POSITION_OFFSET), myFilestream), elevation)
                    End If
                    If USAGETYPE_ENABLED Then
                        'usagetype = readStr(read32(rowoffset + USAGETYPE_POSITION_OFFSET, myFilestream), myFilestream)
                        usagetype = readStr(read32_row(row, USAGETYPE_POSITION_OFFSET), myFilestream)
                    End If

                    obj.IPAddress = myIPAddress
                    obj.IPNumber = ipnum.ToString()
                    obj.CountryShort = country_short
                    obj.CountryLong = country_long
                    obj.Region = region
                    obj.City = city
                    obj.InternetServiceProvider = isp
                    obj.DomainName = domain
                    obj.ZipCode = zipcode
                    obj.NetSpeed = netspeed
                    obj.IDDCode = iddcode
                    obj.AreaCode = areacode
                    obj.WeatherStationCode = weatherstationcode
                    obj.WeatherStationName = weatherstationname
                    obj.TimeZone = timezone
                    obj.Latitude = latitude
                    obj.Longitude = longitude
                    obj.MCC = mcc
                    obj.MNC = mnc
                    obj.MobileBrand = mobilebrand
                    obj.Elevation = elevation
                    obj.UsageType = usagetype

                    obj.Status = If(_HasKey, MSG_OK, MSG_NOT_REGISTERED)

                    Return obj
                    Exit While
                Else
                    'LogDebug.WriteLog("ipnum: " & ipnum.ToString() & " ipfrom: " & ipfrom.ToString())
                    If ipnum < ipfrom Then
                        high = mid - 1
                    Else
                        low = mid + 1
                    End If
                End If
            End While

            obj.Status = "IP_ADDRESS_NOT_FOUND"
            Return obj
        Catch ex As Exception
            LogDebug.WriteLog(ex.Message)
            obj.Status = "UNKNOWN_ERROR"
            Return obj
        Finally
            obj = Nothing
            If Not myFilestream Is Nothing Then
                myFilestream.Close()
                myFilestream.Dispose()
            End If
        End Try
    End Function

    ' Read whole row into array of bytes
    Private Function readrow(ByVal _Pos As Long, ByVal MyLen As UInt32, ByRef MyFilestream As FileStream) As Byte()
        Dim row(MyLen - 1) As Byte
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(row, 0, MyLen)
        Return row
    End Function

    ' Read 8 bits in the encrypted database
    Private Function read8(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As Byte
        Try
            Dim _Byte(0) As Byte
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 1)
            Return _Byte(0)
        Catch ex As Exception
            LogDebug.WriteLog("read8-" & ex.Message)
            Return 0
        End Try
    End Function

    'Private Function read32or128(ByVal _Pos As Long, ByVal _MyIPType As Integer, ByRef MyFilestream As FileStream) As BigInt
    Private Function read32or128(ByVal _Pos As Long, ByVal _MyIPType As Integer, ByRef MyFilestream As FileStream) As IntX
        If _MyIPType = 4 Then
            Return read32(_Pos, MyFilestream)
        ElseIf _MyIPType = 6 Then
            Return read128(_Pos, MyFilestream) ' only IPv6 will run this
        Else
            'Return New BigInt("0")
            Return New IntX()
        End If
    End Function

    ' Read 128 bits in the encrypted database
    'Private Function read128(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As BigInt
    Private Function read128(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            'Dim bigRetVal As BigInt
            Dim bigRetVal As IntX

            Dim _Byte(15) As Byte ' 16 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 16)
            'bigRetVal = New BigInt(System.BitConverter.ToUInt64(_Byte, 8).ToString())
            bigRetVal = New IntX(System.BitConverter.ToUInt64(_Byte, 8).ToString())
            bigRetVal *= SHIFT64BIT
            'bigRetVal += New BigInt(System.BitConverter.ToUInt64(_Byte, 0).ToString())
            bigRetVal += New IntX(System.BitConverter.ToUInt64(_Byte, 0).ToString())

            Return bigRetVal
        Catch ex As Exception
            LogDebug.WriteLog("read128-" & ex.Message)
            'Return New BigInt("0")
            Return New IntX()
        End Try
    End Function

    ' Read 32 bits in byte array
    Private Function read32_row(ByRef row() As Byte, ByVal byteOffset As Integer) As IntX
        Try
            Dim _Byte(3) As Byte ' 4 bytes
            Array.Copy(row, byteOffset, _Byte, 0, 4)

            Return New IntX(System.BitConverter.ToUInt32(_Byte, 0).ToString())
        Catch ex As Exception
            Throw
            LogDebug.WriteLog("read32_row-" & ex.Message)
        End Try
    End Function

    ' Read 32 bits in the encrypted database
    'Private Function read32(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As BigInt
    Private Function read32(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            Dim _Byte(3) As Byte ' 4 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 4)

            'Return New BigInt(System.BitConverter.ToUInt32(_Byte, 0).ToString())
            Return New IntX(System.BitConverter.ToUInt32(_Byte, 0).ToString())
        Catch ex As Exception
            LogDebug.WriteLog("read32-" & ex.Message)
            'Return New BigInt("0")
            Return New IntX()
        End Try
    End Function

    ' Read strings in the encrypted database
    Private Function readStr(ByVal _Pos As Long, ByRef Myfilestream As FileStream) As String
        Try
            Dim _Bytes(0) As Byte
            Dim _Bytes2() As Byte
            Myfilestream.Seek(_Pos, SeekOrigin.Begin)
            Myfilestream.Read(_Bytes, 0, 1)
            ReDim _Bytes2(_Bytes(0) - 1)
            Myfilestream.Read(_Bytes2, 0, _Bytes(0))
            Return System.Text.Encoding.Default.GetString(_Bytes2)
        Catch ex As Exception
            LogDebug.WriteLog("readStr-" & ex.Message)
            Return ""
        End Try
    End Function

    ' Read float number in byte array
    Private Function readFloat_row(ByRef row() As Byte, ByVal byteOffset As Integer) As Single
        Try
            Dim _Byte(3) As Byte
            Array.Copy(row, byteOffset, _Byte, 0, 4)
            Return System.BitConverter.ToSingle(_Byte, 0)
        Catch ex As Exception
            LogDebug.WriteLog("readFloat_row-" & ex.Message)
            Return 0
        End Try
    End Function

    ' Read float number in the encrypted database
    Private Function readFloat(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As Single
        Try
            Dim _Byte(3) As Byte
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 4)
            Return System.BitConverter.ToSingle(_Byte, 0)
        Catch ex As Exception
            LogDebug.WriteLog("readFloat-" & ex.Message)
            Return 0
        End Try
    End Function

    ' Description: Initialize
    Public Sub New()
    End Sub

    ' Description: Validate the IP address input
    'Private Function VerifyIP(ByVal strParam As String, ByRef strIPType As Integer, ByRef ipnum As BigInt) As String
    Private Function VerifyIP(ByVal strParam As String, ByRef strIPType As Integer, ByRef ipnum As IntX) As String
        Try
            Dim address As IPAddress = Nothing
            Dim finalIP As String = ""

            'do checks for outlier cases here
            If _OutlierCase1.IsMatch(strParam) OrElse _OutlierCase2.IsMatch(strParam) Then 'good ip list outliers
                strParam = "0000" & strParam.Substring(1)
            End If

            If Not _OutlierCase3.IsMatch(strParam) AndAlso Not _OutlierCase4.IsMatch(strParam) AndAlso IPAddress.TryParse(strParam, address) Then
                Select Case address.AddressFamily
                    Case System.Net.Sockets.AddressFamily.InterNetwork
                        ' we have IPv4
                        strIPType = 4
                        'Return address.ToString()
                    Case System.Net.Sockets.AddressFamily.InterNetworkV6
                        ' we have IPv6
                        strIPType = 6
                        'Return address.ToString()
                    Case Else
                        Return "Invalid IP"
                End Select

                finalIP = address.ToString().ToUpper()

                'get ip number
                ipnum = IPNo(address)

                If strIPType = 6 Then
                    If ipnum >= _fromBI AndAlso ipnum <= _toBI Then
                        'ipv4-mapped ipv6 should treat as ipv4 and read ipv4 data section
                        strIPType = 4
                        ipnum = ipnum - _fromBI

                        'expand ipv4-mapped ipv6
                        If _IPv4MappedRegex.IsMatch(finalIP) Then
                            finalIP = finalIP.Replace("::", FIVESEGMENTS)
                        ElseIf _IPv4MappedRegex2.IsMatch(finalIP) Then
                            Dim mymatch As RegularExpressions.Match = _IPv4MappedRegex2.Match(finalIP)
                            Dim x As Integer = 0

                            Dim tmp As String = mymatch.Groups(1).ToString()
                            Dim tmparr() As String = tmp.Trim(":").Split(":")
                            Dim len As Integer = tmparr.Length - 1
                            For x = 0 To len
                                tmparr(x) = tmparr(x).PadLeft(4, "0")
                            Next
                            Dim myrear As String = String.Join("", tmparr)
                            Dim bytes As Byte()

                            bytes = BitConverter.GetBytes(Convert.ToInt32("0x" & myrear, 16))
                            finalIP = finalIP.Replace(tmp, ":" & bytes(3) & "." & bytes(2) & "." & bytes(1) & "." & bytes(0))
                            finalIP = finalIP.Replace("::", FIVESEGMENTS)
                        End If
                    ElseIf ipnum >= _FromBI2 AndAlso ipnum <= _ToBI2 Then
                        '6to4 so need to remap to ipv4
                        strIPType = 4

                        ipnum = ipnum >> 80
                        ipnum = ipnum And _DivBI ' get last 32 bits
                    ElseIf ipnum >= _FromBI3 AndAlso ipnum <= _ToBI3 Then
                        'Teredo so need to remap to ipv4
                        strIPType = 4

                        ipnum = Not ipnum
                        ipnum = ipnum And _DivBI ' get last 32 bits
                    ElseIf ipnum <= MAX_IPV4_RANGE Then
                        'ipv4-compatible ipv6 (DEPRECATED BUT STILL SUPPORTED BY .NET)
                        strIPType = 4

                        If _IPv4CompatibleRegex.IsMatch(finalIP) Then
                            Dim bytes As Byte() = BitConverter.GetBytes(Convert.ToInt32(finalIP.Replace("::", "0x"), 16))
                            finalIP = "::" & bytes(3) & "." & bytes(2) & "." & bytes(1) & "." & bytes(0)
                        ElseIf finalIP = "::" Then
                            finalIP = finalIP & "0.0.0.0"
                        End If
                        finalIP = finalIP.Replace("::", FIVESEGMENTS & "FFFF:")
                    Else
                        'expand ipv6 normal
                        Dim myarr() As String = Regex.Split(finalIP, "::")
                        Dim x As Integer = 0
                        Dim leftside As New List(Of String)
                        leftside.AddRange(myarr(0).Split(":"))

                        If myarr.Length > 1 Then
                            Dim rightside As New List(Of String)
                            rightside.AddRange(myarr(1).Split(":"))

                            Dim midarr As List(Of String)
                            midarr = Enumerable.Repeat("0000", 8 - leftside.Count - rightside.Count).ToList

                            rightside.InsertRange(0, midarr)
                            rightside.InsertRange(0, leftside)

                            Dim rlen As Integer = rightside.Count - 1
                            For x = 0 To rlen
                                rightside.Item(x) = rightside.Item(x).PadLeft(4, "0")
                            Next

                            finalIP = String.Join(":", rightside.ToArray())
                        Else
                            Dim llen As Integer = leftside.Count - 1
                            For x = 0 To llen
                                leftside.Item(x) = leftside.Item(x).PadLeft(4, "0")
                            Next

                            finalIP = String.Join(":", leftside.ToArray())
                        End If
                    End If

                End If

                Return finalIP
            Else
                Return "Invalid IP"
            End If
        Catch ex As Exception
            Return "Invalid IP"
        End Try
    End Function

    ' Description: Convert either IPv4 or IPv6 into big integer
    'Private Function IPNo(ByRef ipAddress As IPAddress) As BigInt
    Private Function IPNo(ByRef ipAddress As IPAddress) As IntX
        Try
            Dim addrBytes() As Byte = ipAddress.GetAddressBytes()
            LittleEndian(addrBytes)

            'Dim final As BigInt
            Dim final As IntX

            If addrBytes.Length > 8 Then
                'IPv6
                'final = New BigInt(System.BitConverter.ToUInt64(addrBytes, 8).ToString())
                final = New IntX(System.BitConverter.ToUInt64(addrBytes, 8).ToString())
                final *= SHIFT64BIT
                'final += New BigInt(System.BitConverter.ToUInt64(addrBytes, 0).ToString())
                final += New IntX(System.BitConverter.ToUInt64(addrBytes, 0).ToString())
            Else
                'IPv4
                'final = New BigInt(System.BitConverter.ToUInt32(addrBytes, 0).ToString())
                final = New IntX(System.BitConverter.ToUInt32(addrBytes, 0).ToString())
            End If

            Return final
        Catch ex As Exception
            'Return New BigInt("0")
            Return New IntX()
        End Try
    End Function
End Class