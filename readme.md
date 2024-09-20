# PS2Fas
This is an unoffical PowerShell client for 2FAS Auth.  It was made by connecting to the Browser extension API.

## Build
Built using Visual Studio Community 2022. This project uses several libraries that are included as NuGet deps.
Build this using the following command depending on platform (Only tested on Windows 11 PowerShell 5.1 and 
7.4.5 but might work for other OSs)

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishReadyToRun=true
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishReadyToRun=true
```

Then take the contents of the publish folder and put them into a folder called PS2Fas, then place that folder in
your location for PowerShell modules (Documents\\\<PowerShell or WindowsPowerShell>\\modules)

## Use
After getting your module installed, you must configure the module on the system this is a one time per system
operation.

```
Connect-2Fas
```

This will output a QRCode to the console which you will need to scan with your 2Fas app on your phone like
 the browser extension.  From here you can now call the get OTP cmdlet and get codes for a domain.
the QRCode and keys will be written out to your home directory in a folder called .ps2fas, keep this safe
like an SSH key.

```
Get-2FasOTP -Domain "https://github.com"
```

This will return just your current 6 digit code after the request has been approved on the phone app.
The request will timeout after two minutes if it is rejected or ignored and will return an exception.


## Acknowledgements
This module is made using the following BouncyCastle.Cryptography, Newtonsoft.Json, websocketsharp.core and
QRCoder all of which are MIT licensed and can be downloaded via NuGet.org. Also for 2FAS itself which is a great
app and project.
