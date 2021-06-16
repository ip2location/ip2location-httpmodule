'---------------------------------------------------------------------------
' Author       : IP2Location.com
' URL          : http://www.ip2location.com
' Email        : sales@ip2location.com
'
' Copyright (c) 2002-2021 IP2Location.com
'
' NOTE: Due IIS 7/7.5/8.0/8.5 being able to easily use .NET 3.5 managed module, this component also has been modified to use .NET 3.5
'       and for IPv6 calculations we use IntXLib since .NET 3.5 does not come with BigInteger class.
'---------------------------------------------------------------------------
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports IntXLib ' installed via NuGet

Public NotInheritable Class IP2Location
    Private _DBFilePath As String = ""
    Private _MetaData As MetaData = Nothing
    Private ReadOnly _OutlierCase1 As New Regex("^:(:[\dA-F]{1,4}){7}$", RegexOptions.IgnoreCase)
    Private ReadOnly _OutlierCase2 As New Regex("^:(:[\dA-F]{1,4}){5}:(\d{1,3}\.){3}\d{1,3}$", RegexOptions.IgnoreCase)
    Private ReadOnly _OutlierCase3 As New Regex("^\d+$")
    Private ReadOnly _OutlierCase4 As New Regex("^([\dA-F]{1,4}:){6}(0\d+\.|.*?\.0\d+).*$")
    Private ReadOnly _IPv4MappedRegex As New Regex("^(.*:)((\d+\.){3}\d+)$")
    Private ReadOnly _IPv4MappedRegex2 As New Regex("^.*((:[\dA-F]{1,4}){2})$")
    Private ReadOnly _IPv4CompatibleRegex As New Regex("^::[\dA-F]{1,4}$", RegexOptions.IgnoreCase)
    Private _IPv4ColumnSize As Integer = 0
    Private _IPv6ColumnSize As Integer = 0
    Private ReadOnly _IndexArrayIPv4(65535, 1) As Integer
    Private ReadOnly _IndexArrayIPv6(65535, 1) As Integer

    Private ReadOnly _fromBI As New IntX("281470681743360")
    Private ReadOnly _toBI As New IntX("281474976710655")
    Private ReadOnly _FromBI2 As New IntX("42545680458834377588178886921629466624")
    Private ReadOnly _ToBI2 As New IntX("42550872755692912415807417417958686719")
    Private ReadOnly _FromBI3 As New IntX("42540488161975842760550356425300246528")
    Private ReadOnly _ToBI3 As New IntX("42540488241204005274814694018844196863")
    Private ReadOnly _DivBI As New IntX("4294967295")

    Private Const FIVESEGMENTS As String = "0000:0000:0000:0000:0000:"
    Private ReadOnly SHIFT64BIT As New IntX("18446744073709551616")
    Private ReadOnly MAX_IPV4_RANGE As New IntX("4294967295")
    Private ReadOnly MAX_IPV6_RANGE As New IntX("340282366920938463463374607431768211455")
    Private Const MSG_OK As String = "OK"
    Private Const MSG_INVALID_BIN As String = "Incorrect IP2Location BIN file format. Please make sure that you are using the latest IP2Location BIN file."
    Private Const MSG_NOT_SUPPORTED As String = "This method is not applicable for current IP2Location binary data file. Please upgrade your subscription package to install new data file."
    Private Const MSG_EVALUATION As String = "EVALUATION"
    Private Const MSG_NOT_REGISTERED As String = "NOT_REGISTERED"

    Private ReadOnly COUNTRY_POSITION() As Byte = {0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2}
    Private ReadOnly REGION_POSITION() As Byte = {0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3}
    Private ReadOnly CITY_POSITION() As Byte = {0, 0, 0, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4}
    Private ReadOnly ISP_POSITION() As Byte = {0, 0, 3, 0, 5, 0, 7, 5, 7, 0, 8, 0, 9, 0, 9, 0, 9, 0, 9, 7, 9, 0, 9, 7, 9, 9}
    Private ReadOnly LATITUDE_POSITION() As Byte = {0, 0, 0, 0, 0, 5, 5, 0, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5}
    Private ReadOnly LONGITUDE_POSITION() As Byte = {0, 0, 0, 0, 0, 6, 6, 0, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6}
    Private ReadOnly DOMAIN_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 6, 8, 0, 9, 0, 10, 0, 10, 0, 10, 0, 10, 8, 10, 0, 10, 8, 10, 10}
    Private ReadOnly ZIPCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 7, 7, 7, 0, 7, 7, 7, 0, 7, 0, 7, 7, 7, 0, 7, 7}
    Private ReadOnly TIMEZONE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 8, 7, 8, 8, 8, 7, 8, 0, 8, 8, 8, 0, 8, 8}
    Private ReadOnly NETSPEED_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 11, 0, 11, 8, 11, 0, 11, 0, 11, 0, 11, 11}
    Private ReadOnly IDDCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 12, 0, 12, 0, 12, 9, 12, 0, 12, 12}
    Private ReadOnly AREACODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 13, 0, 13, 0, 13, 10, 13, 0, 13, 13}
    Private ReadOnly WEATHERSTATIONCODE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 14, 0, 14, 0, 14, 0, 14, 14}
    Private ReadOnly WEATHERSTATIONNAME_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 15, 0, 15, 0, 15, 0, 15, 15}
    Private ReadOnly MCC_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 16, 0, 16, 9, 16, 16}
    Private ReadOnly MNC_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 17, 0, 17, 10, 17, 17}
    Private ReadOnly MOBILEBRAND_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 18, 0, 18, 11, 18, 18}
    Private ReadOnly ELEVATION_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 19, 0, 19, 19}
    Private ReadOnly USAGETYPE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 20, 20}
    Private ReadOnly ADDRESSTYPE_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 21}
    Private ReadOnly CATEGORY_POSITION() As Byte = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22}

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
    Private ADDRESSTYPE_POSITION_OFFSET As Integer = 0
    Private CATEGORY_POSITION_OFFSET As Integer = 0

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
    Private ADDRESSTYPE_ENABLED As Boolean = False
    Private CATEGORY_ENABLED As Boolean = False

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
        Return True
    End Function

    ' Description: Read BIN file
    Private Function LoadBIN() As Boolean
        Dim loadOK As Boolean = False
        Try
            If _DBFilePath <> "" Then
                Using myFileStream As New FileStream(_DBFilePath, FileMode.Open, FileAccess.Read)
                    _MetaData = New MetaData
                    With _MetaData
                        .DBType = Read8(1, myFileStream)
                        .DBColumn = Read8(2, myFileStream)
                        .DBYear = Read8(3, myFileStream)
                        .DBMonth = Read8(4, myFileStream)
                        .DBDay = Read8(5, myFileStream)
                        .DBCount = Read32(6, myFileStream) '4 bytes
                        .BaseAddr = Read32(10, myFileStream) '4 bytes
                        .DBCountIPv6 = Read32(14, myFileStream) '4 bytes
                        .BaseAddrIPv6 = Read32(18, myFileStream) '4 bytes
                        .IndexBaseAddr = Read32(22, myFileStream) '4 bytes
                        .IndexBaseAddrIPv6 = Read32(26, myFileStream) '4 bytes
                        .ProductCode = Read8(30, myFileStream)
                        ' below 2 fields just read for now, not being used yet
                        .ProductType = Read8(31, myFileStream)
                        .FileSize = Read32(32, myFileStream) '4 bytes

                        ' check if is correct BIN (should be 1 for IP2Location BIN file), also checking for zipped file (PK being the first 2 chars)
                        If (.ProductCode <> 1 AndAlso .DBYear >= 21) OrElse (.DBType = 80 AndAlso .DBColumn = 75) Then ' only BINs from Jan 2021 onwards have this byte set
                            Throw New Exception(MSG_INVALID_BIN)
                        End If

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
                        ADDRESSTYPE_POSITION_OFFSET = If(ADDRESSTYPE_POSITION(dbt) <> 0, (ADDRESSTYPE_POSITION(dbt) - 2) << 2, 0)
                        CATEGORY_POSITION_OFFSET = If(CATEGORY_POSITION(dbt) <> 0, (CATEGORY_POSITION(dbt) - 2) << 2, 0)

                        COUNTRY_ENABLED = COUNTRY_POSITION(dbt) <> 0
                        REGION_ENABLED = REGION_POSITION(dbt) <> 0
                        CITY_ENABLED = CITY_POSITION(dbt) <> 0
                        ISP_ENABLED = ISP_POSITION(dbt) <> 0
                        LATITUDE_ENABLED = LATITUDE_POSITION(dbt) <> 0
                        LONGITUDE_ENABLED = LONGITUDE_POSITION(dbt) <> 0
                        DOMAIN_ENABLED = DOMAIN_POSITION(dbt) <> 0
                        ZIPCODE_ENABLED = ZIPCODE_POSITION(dbt) <> 0
                        TIMEZONE_ENABLED = TIMEZONE_POSITION(dbt) <> 0
                        NETSPEED_ENABLED = NETSPEED_POSITION(dbt) <> 0
                        IDDCODE_ENABLED = IDDCODE_POSITION(dbt) <> 0
                        AREACODE_ENABLED = AREACODE_POSITION(dbt) <> 0
                        WEATHERSTATIONCODE_ENABLED = WEATHERSTATIONCODE_POSITION(dbt) <> 0
                        WEATHERSTATIONNAME_ENABLED = WEATHERSTATIONNAME_POSITION(dbt) <> 0
                        MCC_ENABLED = MCC_POSITION(dbt) <> 0
                        MNC_ENABLED = MNC_POSITION(dbt) <> 0
                        MOBILEBRAND_ENABLED = MOBILEBRAND_POSITION(dbt) <> 0
                        ELEVATION_ENABLED = ELEVATION_POSITION(dbt) <> 0
                        USAGETYPE_ENABLED = USAGETYPE_POSITION(dbt) <> 0
                        ADDRESSTYPE_ENABLED = ADDRESSTYPE_POSITION(dbt) <> 0
                        CATEGORY_ENABLED = CATEGORY_POSITION(dbt) <> 0

                        If .Indexed Then
                            Dim pointer As Integer = .IndexBaseAddr

                            ' read IPv4 index
                            For x As Integer = _IndexArrayIPv4.GetLowerBound(0) To _IndexArrayIPv4.GetUpperBound(0)
                                _IndexArrayIPv4(x, 0) = Read32(pointer, myFileStream) '4 bytes for from row
                                _IndexArrayIPv4(x, 1) = Read32(pointer + 4, myFileStream) '4 bytes for to row
                                pointer += 8
                            Next

                            If .IndexedIPv6 Then
                                ' read IPv6 index
                                For x As Integer = _IndexArrayIPv6.GetLowerBound(0) To _IndexArrayIPv6.GetUpperBound(0)
                                    _IndexArrayIPv6(x, 0) = Read32(pointer, myFileStream) '4 bytes for from row
                                    _IndexArrayIPv6(x, 1) = Read32(pointer + 4, myFileStream) '4 bytes for to row
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
        If BitConverter.IsLittleEndian Then
            Dim byteList As New List(Of Byte)(byteArr)
            byteList.Reverse()
            byteArr = byteList.ToArray()
        End If
    End Sub

    ' Description: Query database to get location information by IP address
    Public Function IPQuery(ByVal myIPAddress As String) As IPResult
        Dim obj As New IPResult
        Dim strIP As String
        Dim myIPType As Integer = 0
        Dim myDBType As Integer
        Dim myBaseAddr As Integer = 0
        Dim myDBColumn As Integer
        Dim myFilestream As FileStream = Nothing

        Dim countrypos As Long
        Dim low As Long = 0
        Dim high As Long = 0
        Dim mid As Long
        Dim ipfrom As IntX
        Dim ipto As IntX
        Dim ipnum As New IntX()
        Dim indexaddr As Long
        Dim MAX_IP_RANGE As New IntX()
        Dim rowoffset As Long
        Dim rowoffset2 As Long
        Dim myColumnSize As Integer = 0

        Try
            If myIPAddress = "" OrElse myIPAddress Is Nothing Then
                obj.Status = "EMPTY_IP_ADDRESS"
                Return obj
            End If

            strIP = Me.VerifyIP(myIPAddress, myIPType, ipnum)
            If strIP <> "Invalid IP" Then
                myIPAddress = strIP
            Else
                obj.Status = "INVALID_IP_ADDRESS"
                Return obj
            End If

            ' Read BIN if haven't done so
            If _MetaData Is Nothing Then
                If Not LoadBIN() Then ' problems reading BIN
                    obj.Status = "MISSING_FILE"
                    Return obj
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
                        indexaddr = ipnum >> 16
                        low = _IndexArrayIPv4(indexaddr, 0)
                        high = _IndexArrayIPv4(indexaddr, 1)
                    End If
                Case 6
                    ' IPv6
                    If _MetaData.OldBIN Then ' old IPv4-only BIN don't contain IPv6 data
                        obj.Status = "IPV6_NOT_SUPPORTED"
                        Return obj
                    End If
                    MAX_IP_RANGE = MAX_IPV6_RANGE
                    high = _MetaData.DBCountIPv6
                    myBaseAddr = _MetaData.BaseAddrIPv6
                    myColumnSize = _IPv6ColumnSize

                    If _MetaData.IndexedIPv6 Then
                        indexaddr = ipnum >> 112
                        low = _IndexArrayIPv6(indexaddr, 0)
                        high = _IndexArrayIPv6(indexaddr, 1)
                    End If
            End Select

            If ipnum >= MAX_IP_RANGE Then
                ipnum = MAX_IP_RANGE - New IntX(1)
            End If

            While (low <= high)
                mid = CInt((low + high) / 2)

                rowoffset = myBaseAddr + (mid * myColumnSize)
                rowoffset2 = rowoffset + myColumnSize

                ipfrom = Read32or128(rowoffset, myIPType, myFilestream)
                ipto = Read32or128(rowoffset2, myIPType, myFilestream)

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
                    Dim addresstype As String = MSG_NOT_SUPPORTED
                    Dim category As String = MSG_NOT_SUPPORTED

                    Dim firstCol As Integer = 4 ' for IPv4, IP From is 4 bytes
                    If myIPType = 6 Then ' IPv6
                        firstCol = 16 ' 16 bytes for IPv6
                    End If

                    ' read the row here after the IP From column (remaining columns are all 4 bytes)
                    Dim row() As Byte = ReadRow(rowoffset + firstCol, myColumnSize - firstCol, myFilestream)

                    If COUNTRY_ENABLED Then
                        countrypos = Read32FromRow(row, COUNTRY_POSITION_OFFSET)
                        country_short = ReadStr(countrypos, myFilestream)
                        country_long = ReadStr(countrypos + 3, myFilestream)
                    End If
                    If REGION_ENABLED Then
                        region = ReadStr(Read32FromRow(row, REGION_POSITION_OFFSET), myFilestream)
                    End If
                    If CITY_ENABLED Then
                        city = ReadStr(Read32FromRow(row, CITY_POSITION_OFFSET), myFilestream)
                    End If
                    If ISP_ENABLED Then
                        isp = ReadStr(Read32FromRow(row, ISP_POSITION_OFFSET), myFilestream)
                    End If
                    If DOMAIN_ENABLED Then
                        domain = ReadStr(Read32FromRow(row, DOMAIN_POSITION_OFFSET), myFilestream)
                    End If
                    If ZIPCODE_ENABLED Then
                        zipcode = ReadStr(Read32FromRow(row, ZIPCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If LATITUDE_ENABLED Then
                        latitude = ReadFloatFromRow(row, LATITUDE_POSITION_OFFSET)
                    End If
                    If LONGITUDE_ENABLED Then
                        longitude = ReadFloatFromRow(row, LONGITUDE_POSITION_OFFSET)
                    End If
                    If TIMEZONE_ENABLED Then
                        timezone = ReadStr(Read32FromRow(row, TIMEZONE_POSITION_OFFSET), myFilestream)
                    End If
                    If NETSPEED_ENABLED Then
                        netspeed = ReadStr(Read32FromRow(row, NETSPEED_POSITION_OFFSET), myFilestream)
                    End If
                    If IDDCODE_ENABLED Then
                        iddcode = ReadStr(Read32FromRow(row, IDDCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If AREACODE_ENABLED Then
                        areacode = ReadStr(Read32FromRow(row, AREACODE_POSITION_OFFSET), myFilestream)
                    End If
                    If WEATHERSTATIONCODE_ENABLED Then
                        weatherstationcode = ReadStr(Read32FromRow(row, WEATHERSTATIONCODE_POSITION_OFFSET), myFilestream)
                    End If
                    If WEATHERSTATIONNAME_ENABLED Then
                        weatherstationname = ReadStr(Read32FromRow(row, WEATHERSTATIONNAME_POSITION_OFFSET), myFilestream)
                    End If
                    If MCC_ENABLED Then
                        mcc = ReadStr(Read32FromRow(row, MCC_POSITION_OFFSET), myFilestream)
                    End If
                    If MNC_ENABLED Then
                        mnc = ReadStr(Read32FromRow(row, MNC_POSITION_OFFSET), myFilestream)
                    End If
                    If MOBILEBRAND_ENABLED Then
                        mobilebrand = ReadStr(Read32FromRow(row, MOBILEBRAND_POSITION_OFFSET), myFilestream)
                    End If
                    If ELEVATION_ENABLED Then
                        Single.TryParse(ReadStr(Read32FromRow(row, ELEVATION_POSITION_OFFSET), myFilestream), elevation)
                    End If
                    If USAGETYPE_ENABLED Then
                        usagetype = ReadStr(Read32FromRow(row, USAGETYPE_POSITION_OFFSET), myFilestream)
                    End If
                    If ADDRESSTYPE_ENABLED Then
                        addresstype = ReadStr(Read32FromRow(row, ADDRESSTYPE_POSITION_OFFSET), myFilestream)
                    End If
                    If CATEGORY_ENABLED Then
                        category = ReadStr(Read32FromRow(row, CATEGORY_POSITION_OFFSET), myFilestream)
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
                    obj.AddressType = addresstype
                    obj.Category = category

                    obj.Status = MSG_OK

                    Return obj
                Else
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
            If myFilestream IsNot Nothing Then
                myFilestream.Close()
                myFilestream.Dispose()
            End If
        End Try
    End Function

    ' Read whole row into array of bytes
    Private Function ReadRow(ByVal _Pos As Long, ByVal MyLen As UInt32, ByRef MyFilestream As FileStream) As Byte()
        Dim row(MyLen - 1) As Byte
        MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
        MyFilestream.Read(row, 0, MyLen)
        Return row
    End Function

    ' Read 8 bits in the database
    Private Function Read8(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As Byte
        Try
            Dim _Byte(0) As Byte
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 1)
            Return _Byte(0)
        Catch ex As Exception
            LogDebug.WriteLog("Read8-" & ex.Message)
            Return 0
        End Try
    End Function

    Private Function Read32or128(ByVal _Pos As Long, ByVal _MyIPType As Integer, ByRef MyFilestream As FileStream) As IntX
        If _MyIPType = 4 Then
            Return Read32(_Pos, MyFilestream)
        ElseIf _MyIPType = 6 Then
            Return Read128(_Pos, MyFilestream)
        Else
            Return New IntX()
        End If
    End Function

    ' Read 128 bits in the database
    Private Function Read128(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            Dim bigRetVal As IntX

            Dim _Byte(15) As Byte ' 16 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 16)
            bigRetVal = New IntX(BitConverter.ToUInt64(_Byte, 8).ToString())
            bigRetVal *= SHIFT64BIT
            bigRetVal += New IntX(BitConverter.ToUInt64(_Byte, 0).ToString())

            Return bigRetVal
        Catch ex As Exception
            LogDebug.WriteLog("Read128-" & ex.Message)
            Return New IntX()
        End Try
    End Function

    ' Read 32 bits in byte array
    Private Function Read32FromRow(ByRef row() As Byte, ByVal byteOffset As Integer) As IntX
        Try
            Dim _Byte(3) As Byte ' 4 bytes
            Array.Copy(row, byteOffset, _Byte, 0, 4)

            Return New IntX(BitConverter.ToUInt32(_Byte, 0).ToString())
        Catch ex As Exception
            LogDebug.WriteLog("Read32FromRow-" & ex.Message)
            Throw
        End Try
    End Function

    ' Read 32 bits in the database
    Private Function Read32(ByVal _Pos As Long, ByRef MyFilestream As FileStream) As IntX
        Try
            Dim _Byte(3) As Byte ' 4 bytes
            MyFilestream.Seek(_Pos - 1, SeekOrigin.Begin)
            MyFilestream.Read(_Byte, 0, 4)

            Return New IntX(BitConverter.ToUInt32(_Byte, 0).ToString())
        Catch ex As Exception
            LogDebug.WriteLog("Read32-" & ex.Message)
            Return New IntX()
        End Try
    End Function

    ' Read strings in the database
    Private Function ReadStr(ByVal _Pos As Long, ByRef Myfilestream As FileStream) As String
        Try
            Dim _Bytes(0) As Byte
            Dim _Bytes2() As Byte
            Myfilestream.Seek(_Pos, SeekOrigin.Begin)
            Myfilestream.Read(_Bytes, 0, 1)
            ReDim _Bytes2(_Bytes(0) - 1)
            Myfilestream.Read(_Bytes2, 0, _Bytes(0))
            Return Encoding.Default.GetString(_Bytes2)
        Catch ex As Exception
            LogDebug.WriteLog("ReadStr-" & ex.Message)
            Return ""
        End Try
    End Function

    ' Read float number in byte array
    Private Function ReadFloatFromRow(ByRef row() As Byte, ByVal byteOffset As Integer) As Single
        Try
            Dim _Byte(3) As Byte
            Array.Copy(row, byteOffset, _Byte, 0, 4)
            Return BitConverter.ToSingle(_Byte, 0)
        Catch ex As Exception
            LogDebug.WriteLog("ReadFloatFromRow-" & ex.Message)
            Return 0
        End Try
    End Function

    ' Description: Initialize
    Public Sub New()
    End Sub

    ' Description: Validate the IP address input
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
                    Case Sockets.AddressFamily.InterNetwork
                        strIPType = 4
                    Case Sockets.AddressFamily.InterNetworkV6
                        strIPType = 6
                    Case Else
                        Return "Invalid IP"
                End Select

                finalIP = address.ToString().ToUpper()

                ipnum = IPNo(address)

                If strIPType = 6 Then
                    If ipnum >= _fromBI AndAlso ipnum <= _toBI Then
                        'ipv4-mapped ipv6 should treat as ipv4 and read ipv4 data section
                        strIPType = 4
                        ipnum -= _fromBI

                        'expand ipv4-mapped ipv6
                        If _IPv4MappedRegex.IsMatch(finalIP) Then
                            finalIP = finalIP.Replace("::", FIVESEGMENTS)
                        ElseIf _IPv4MappedRegex2.IsMatch(finalIP) Then
                            Dim mymatch As Match = _IPv4MappedRegex2.Match(finalIP)
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

                        ipnum >>= 80
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
                            finalIP &= "0.0.0.0"
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
    Private Function IPNo(ByRef ipAddress As IPAddress) As IntX
        Try
            Dim addrBytes() As Byte = ipAddress.GetAddressBytes()
            LittleEndian(addrBytes)

            Dim final As IntX

            If addrBytes.Length > 8 Then
                'IPv6
                final = New IntX(BitConverter.ToUInt64(addrBytes, 8).ToString())
                final *= SHIFT64BIT
                final += New IntX(BitConverter.ToUInt64(addrBytes, 0).ToString())
            Else
                'IPv4
                final = New IntX(BitConverter.ToUInt32(addrBytes, 0).ToString())
            End If

            Return final
        Catch ex As Exception
            Return New IntX()
        End Try
    End Function
End Class