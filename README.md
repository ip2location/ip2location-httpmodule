# IP2Location IP Geolocation HTTP Module

This IIS managed module allows user to get geolocation information about an IP address such as country of origin, region, city, latitude, longitude, ZIP code, ISP, domain name, time zone, connection speed, IDD code, area code, weather station code, and weather station name, mobile country code (MCC), mobile network code (MNC), carrier brand, usage type, elevation, address type, IAB category, district, autonomous system number (ASN) and autonomous system (AS). It lookup the IP address from **IP2Location BIN Data** file. This data file can be downloaded at

* Free IP2Location IP geolocation BIN Data: https://lite.ip2location.com
* Commercial IP2Location IP geolocation BIN Data: https://www.ip2location.com/database/ip2location


## Requirements

* Visual Studio 2010 or later.
* Microsoft .NET 3.5 framework.
* [IntX](https://www.nuget.org/packages/IntX/)
* [Microsoft ILMerge](https://www.microsoft.com/en-my/download/details.aspx?id=17630)

Supported Microsoft IIS Versions: 7.0, 7.5, 8.0, 8.5, 10.0 (website needs to be running under a .NET 2.0 application pool in integrated mode)


## Compilation

Just open the solution file in Visual Studio and compile. Or just use the IP2LocationHTTPModule.dll in the dll folder.

**NOTE: After compilation, the final IP2LocationHTTPModule.dll will be in the merged folder as the post-build event will merge the IntXLib.dll with the original IP2LocationHTTPModule.dll to make it easier for deployment.**

___

## Installation & Configuration

**NOTE: You can choose to install the IP2Location IP Geolocation HTTP Module in either per website mode or per server mode.**

If you install in per website mode, you will need to install and configure for every website that you wish to add the IP2Location feature.
If you install in per server mode, you just need to install and configure once and all websites hosted on that machine will be able to use IP2Location.

### Installation & Configuration (per website mode)

1. Copy the IP2LocationHTTPModule.dll, IP2Location-config.xml and the BIN data file to the bin folder of your website.

2. Modify your web.config as below:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <modules runAllManagedModulesForAllRequests="true">
            <add name="IP2LocationModule" type="IP2Location.HTTPModule" />
        </modules>
    </system.webServer>
</configuration>
```

3. Open the IP2Location-config.xml file in your bin folder using any text editor and you can see the below:

```xml
<?xml version="1.0" encoding="utf-8"?>
<IP2Location_Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Settings>
    <BIN_File>bin\your_database_file.BIN</BIN_File>
    <Company_Name></Company_Name>
    <License_Key></License_Key>
    <Custom_IP_Server_Variable>HTTP_X_FORWARDED_FOR</Custom_IP_Server_Variable>
    <Enabled_Server_Variables>True</Enabled_Server_Variables>
  </Settings>
  <ByPassIPs>
    <ByPassIP>
      <IP>1.2.3.4</IP>
    </ByPassIP>
  </ByPassIPs>
  <BlockRules>
    <BlockRule>
      <URL_Regex>.*\.php</URL_Regex>
      <Comparison>NOT IN</Comparison>
      <Countries>US,CA,MY</Countries>
      <Enabled_Rule>True</Enabled_Rule>
    </BlockRule>
  </BlockRules>
  <RedirectRules>
    <RedirectRule>
      <URL_Regex>.*/Default\.aspx</URL_Regex>
      <Redirect_To_URL>http://www.google.sg</Redirect_To_URL>
      <Comparison>IN</Comparison>
      <Countries>MY,SG,AU</Countries>
      <Enabled_Rule>True</Enabled_Rule>
    </RedirectRule>
  </RedirectRules>
</IP2Location_Configuration>
```

What should I change? (ByPassIPs, BlockRules & RedirectRules are optional if you do not require them)
<ul>
<li>&lt;BIN_File>The relative path for the BIN database file that you have copied into the bin folder of your website. (relative to the root of your website)</li>
<li>&lt;Company_Name>No longer required, just leave this blank</li>
<li>&lt;License_Key>No longer required, just leave this blank</li>
<li>&lt;Custom_IP_Server_Variable>Leave blank unless you need to read the IP from a custom field.</li>
<li>&lt;Enabled_Server_Variables>When the value is True, your webpages can have access to IP Geolocation data for your visitor’s IP address via server variables. You can turn this off by changing the value to False.</li>
<li>Under &lt;ByPassIPs>, each &lt;ByPassIP>is used to configure an IP address to bypass the block/redirect rules. Just add another &lt;ByPassIP> segment if you want to have another IP address bypass the rules.</li>
<li>Under &lt;BlockRules>, each &lt;BlockRule>is used to configure a rule for blocking visitor access to the website. Just add another &lt;BlockRule> segment if you want another rule.</li>
<li>&lt;URL_Regex>A regular expression string to match various pages and sub-folders.</li>
<li>&lt;Comparison>Allows 2 values; either IN or NOT IN. In the case of IN, if the visitor’s country is in the &lt;Countries> list for that rule and the &lt;URL_Regex> matches the browsed URL then the visitor will be blocked via a HTTP 403 status.</li>
<li>&lt;Enabled_Rule>Accepts either True or False and this just turns on or off the rule.</li>
<li>&lt;RedirectRules> and &lt;RedirectRule> works similar to the &lt;BlockRules>, except the visitor is redirected to the URL specified in &lt;Redirect_To_URL> instead of being blocked.</li>
</ul>

### Installation & Configuration (per server mode)

1. Create a new folder.

2. Copy the IP2LocationHTTPModule.dll, IP2Location-config.xml and the BIN data file to that folder.

3. Create a Windows environment system variable to store the path of the new folder.
   1. Open the Control Panel then double-click on System then click on Advanced System Settings.
   2. Click on the Environment Variables button to open up the Environment Variable settings.
   3. Under System variables, create a new variable called IP2LocationHTTPModuleConfig and set the value to the full path of the new folder.

4. Create a PowerShell script called installgac.ps1 and paste the following code into it.

```powershell
Set-location "C:\<new folder>"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacInstall("C:\<new folder>\IP2LocationHTTPModule.dll")
iisreset
```

5. Create a PowerShell script called uninstallgac.ps1 and paste the following code into it.

```powershell
Set-location "C:\<new folder>"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish
$publish.GacRemove("C:\<new folder>\IP2LocationHTTPModule.dll")
iisreset
```

6. In both scripts, edit the 2 lines containing the path to the full path for your new folder then save the scripts.

7. Run installgac.ps1 to install the dll into the GAC. Keep the uninstallgac.ps1 in case you need to uninstall the dll. 

8. Installing the module in IIS.
   1. Open the IIS Manager then navigate to the server level settings and double-click on the Modules icon.
   2. In the Modules settings, click on the Add Managed Module at the right-hand side.
   3. Key in IP2LocationHTTPModule for the Name and select IP2Location.HTTPModule as the Type.
   4. Click OK then restart IIS to complete the installation.

9. Open the IP2Location-config.xml in your new folder using any text editor. Fill in the &lt;BIN_File> tag with the absolute path to your BIN data file and remove the HTTP_X_FORWARDED_FOR if your website is not behind a proxy. Save your changes.

```xml
<?xml version="1.0" encoding="utf-8"?>
<IP2Location_Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Settings>
    <BIN_File>C:\<folder>\<BIN filename></BIN_File>
    <Company_Name></Company_Name>
    <License_Key></License_Key>
    <Custom_IP_Server_Variable>HTTP_X_FORWARDED_FOR</Custom_IP_Server_Variable>
    <Enabled_Server_Variables>True</Enabled_Server_Variables>
  </Settings>
</IP2Location_Configuration>
```

___

## Usage

Below are the server variables set by the IP2Location HTTP Module. You can use any programming languages to read the server variables.

|Variable Name|Description|
|---|---|
|HTTP_X_COUNTRY_SHORT|Two-character country code based on ISO 3166.|
|HTTP_X_COUNTRY_LONG|Country name based on ISO 3166.|
|HTTP_X_COUNTRY_REGION|Region or state name.|
|HTTP_X_COUNTRY_CITY|City name.|
|HTTP_X_COUNTRY_LATITUDE|City latitude.|
|HTTP_X_COUNTRY_LONGITUDE|City longitude.|
|HTTP_X_COUNTRY_ZIPCODE|ZIP or Postal Code.|
|HTTP_X_COUNTRY_TIMEZONE|UTC time zone.|
|HTTP_X_COUNTRY_ISP|Internet Service Provider or company's name.|
|HTTP_X_COUNTRY_DOMAIN|Internet domain name associated to IP address range.|
|HTTP_X_COUNTRY_NETSPEED|Internet connection type. DIAL = dial up, DSL = broadband/cable, COMP = company/T1|
|HTTP_X_COUNTRY_IDD_CODE|The IDD prefix to call the city from another country.|
|HTTP_X_COUNTRY_AREA_CODE|A varying length number assigned to geographic areas for call between cities.|
|HTTP_X_COUNTRY_WEATHER_CODE|The special code to identify the nearest weather observation station.|
|HTTP_X_COUNTRY_WEATHER_NAME|The name of the nearest weather observation station.|
|HTTP_X_COUNTRY_MCC|Mobile Country Codes (MCC) as defined in ITU E.212 for use in identifying mobile stations in wireless telephone networks, particularly GSM and UMTS networks.|
|HTTP_X_COUNTRY_MNC|Mobile Network Code (MNC) is used in combination with a Mobile Country Code (MCC) to uniquely identify a mobile phone operator or carrier.|
|HTTP_X_COUNTRY_MOBILE_BRAND|Commercial brand associated with the mobile carrier.|
|HTTP_X_COUNTRY_ELEVATION|Average height of city above sea water in meters (m).|
|HTTP_X_COUNTRY_USAGE_TYPE|Usage type classification of ISP or company:<ul><li>(COM) Commercial</li><li>(ORG) Organization</li><li>(GOV) Government</li><li>(MIL) Military</li><li>(EDU) University/College/School</li><li>(LIB) Library</li><li>(CDN) Content Delivery Network</li><li>(ISP) Fixed Line ISP</li><li>(MOB) Mobile ISP</li><li>(DCH) Data Center/Web Hosting/Transit</li><li>(SES) Search Engine Spider</li><li>(RSV) Reserved</li></ul>|
|HTTP_X_COUNTRY_ADDRESS_TYPE|IP address types as defined in Internet Protocol version 4 (IPv4) and Internet Protocol version 6 (IPv6).<ul><li>(A) Anycast - One to the closest</li><li>(U) Unicast - One to one</li><li>(M) Multicast - One to multiple</li><li>(B) Broadcast - One to all</li></ul>|
|HTTP_X_COUNTRY_CATEGORY|The domain category is based on [IAB Tech Lab Content Taxonomy](https://www.ip2location.com/free/iab-categories). These categories are comprised of Tier-1 and Tier-2 (if available) level categories widely used in services like advertising, Internet security and filtering appliances.|
|HTTP_X_COUNTRY_DISTRICT|District.|
|HTTP_X_COUNTRY_ASN|Autonomous System Number.|
|HTTP_X_COUNTRY_AS|Autonomous System.|
|HTTP_X_COUNTRY_AS_DOMAIN|Autonomous System domain name.|
|HTTP_X_COUNTRY_AS_USAGE_TYPE|Autonomous System usage type.|
|HTTP_X_COUNTRY_AS_CIDR|Autonomous System CIDR.|
___

## Sample Codes

### ASP.NET (VB)

```vb.net
Private Sub ShowServerVariable()
    Response.Write(Request.ServerVariables("REMOTE_ADDR") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_SHORT") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_LONG") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_REGION") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_CITY") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_LATITUDE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_LONGITUDE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_ZIPCODE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_TIMEZONE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_ISP") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_DOMAIN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_NETSPEED") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_IDD_CODE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_AREA_CODE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_WEATHER_CODE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_WEATHER_NAME") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_MCC") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_MNC") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_MOBILE_BRAND") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_ELEVATION") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_USAGE_TYPE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_ADDRESS_TYPE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_CATEGORY") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_DISTRICT") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_ASN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_AS") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_AS_DOMAIN") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_AS_USAGE_TYPE") & "<br>")
    Response.Write(Request.ServerVariables("HTTP_X_COUNTRY_AS_CIDR") & "<br>")
End Sub
```

### ASP.NET (C#)

```csharp
private void ShowServerVariable()
{
   Response.Write(Request.ServerVariables["REMOTE_ADDR"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_SHORT"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_LONG"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_REGION"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_CITY"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_LATITUDE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_LONGITUDE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_ZIPCODE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_TIMEZONE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_ISP"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_DOMAIN"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_NETSPEED"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_IDD_CODE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_AREA_CODE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_WEATHER_CODE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_WEATHER_NAME"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_MCC"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_MNC"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_MOBILE_BRAND"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_ELEVATION"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_USAGE_TYPE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_ADDRESS_TYPE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_CATEGORY"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_DISTRICT"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_ASN"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_AS"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_AS_DOMAIN"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_AS_USAGE_TYPE"] + "\n");
   Response.Write(Request.ServerVariables["HTTP_X_COUNTRY_AS_CIDR"] + "\n");
}
```

### ASP

```asp
<html>
<head>
    <title>IP2Location HTTP Module</title>
</head>
<body>
    <%=Request.ServerVariables("REMOTE_ADDR") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_SHORT") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_LONG") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_REGION") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_CITY") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_LATITUDE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_LONGITUDE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_ZIPCODE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_TIMEZONE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_ISP") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_DOMAIN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_NETSPEED") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_IDD_CODE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_AREA_CODE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_WEATHER_CODE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_WEATHER_NAME") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_MCC") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_MNC") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_MOBILE_BRAND") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_ELEVATION") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_USAGE_TYPE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_ADDRESS_TYPE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_CATEGORY") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_DISTRICT") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_ASN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_AS") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_AS_DOMAIN") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_AS_USAGE_TYPE") & "<br>"%>
    <%=Request.ServerVariables("HTTP_X_COUNTRY_AS_CIDR") & "<br>"%>
</body>
</html>
```

### PHP

```php
<html>
<head>
    <title>IP2Location HTTP Module</title>
</head>
<body>
<?php
    echo $_SERVER['REMOTE_ADDR'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_SHORT'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_LONG'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_REGION'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_CITY'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_LATITUDE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_LONGITUDE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_ZIPCODE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_TIMEZONE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_ISP'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_DOMAIN'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_NETSPEED'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_IDD_CODE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_AREA_CODE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_WEATHER_CODE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_WEATHER_NAME'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_MCC'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_MNC'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_MOBILE_BRAND'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_ELEVATION'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_USAGE_TYPE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_ADDRESS_TYPE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_CATEGORY'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_DISTRICT'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_ASN'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_AS'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_AS_DOMAIN'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_AS_USAGE_TYPE'] . "<br>";
    echo $_SERVER['HTTP_X_COUNTRY_AS_CIDR'] . "<br>";
?>
</body>
</html>
```
