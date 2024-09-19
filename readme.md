# PS2Fas
This is an unoffical PowerShell client for 2FAS Auth.  It was made by connecting to the Browser extention API.

## Build
Built using Visual Studio Community 2022. This project uses several libaries that are included as NuGet deps.
Build this using the following command depending on platform (Only tested on Windows 11 Powershell 5.1 and 
7.4.5 but might work for other OSs)

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishReadyToRun=true
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishReadyToRun=true
```

Then take the contents of the publish folder and put them into a folder called PS2Fas, then place that folder in
your location for PowerShell modules (Documents\\\<PowerShell or WindowsPowerShell>\\modules)

## Aknowelgements
This module is made using the following BouncyCastle.Cryptography, Newtonsoft.Json, websocketsharp.core and
QRCoder all of which are MIT licensed and can be downloaded via NuGet.org. Also for 2FAS itself which is a great
app and project.
