# PS2Fas
This is an unoffical PowerShell client for 2FAS Auth.  It was made by connecting to the Browser extension API.

## Build
Built using Visual Studio Community 2022. This project uses several libraries that are included as NuGet deps.
Build this using the following command. Tested on Windows and Linux, might work on Macs?

Tested on:\
Windows 11 x64 PowerShell 5.1 and 7.4.5\
Ubuntu 22.04 PowerShell 7.4.5

```
dotnet publish -c Release -f netstandard2.0 --self-contained true -p:PublishReadyToRun=true
```

Then take the contents of the publish folder and put them into a folder called PS2Fas, then place that folder in
your location for PowerShell modules

Windows: Documents\\\<PowerShell or WindowsPowerShell>\\Modules
Linux: ~/.local/share/powershell/Modules

## Use
After getting your module installed, you must configure the module on the system this is a one time per system
operation.

```
Connect-2Fas
```

This will output a QRCode to the console which you will need to scan with your 2Fas app on your phone like
 the browser extension.  From here you can now call the get OTP cmdlet and get codes for a domain.
the QRCode and keys will be written out to your home directory in a folder called .ps2fas, keep this safe
like an SSH key.  In the event you were unable to scan the QRCode in the console it is saved into a text file in
the .ps2fas directory.  Open in an text editor with a mono space font set, then scan with phone.

```
Get-2FasOTP -Domain "https://github.com"
```

This will return just your current 6 digit code after the request has been approved on the phone app.
The request will timeout after two minutes if it is rejected or ignored and will return an exception.
The timeout can be changed by passing -Timeout and a number of minutes (ex. ```-Timeout 5```)  Domain
must be a well formated URL or the prompt on your 2FAS will be empty.  At a later time I may add some
code to deal with this so anything can be entered and converted into a valid non routeable URL usable for 
autosending tokens.

## Acknowledgements
This module is made using the following BouncyCastle.Cryptography, Newtonsoft.Json, websocketsharp.core and
QRCoder all of which are MIT licensed and can be downloaded via NuGet.org.\
Thanks to 2FAS itself which is a great app and project. No 2FAS source was used in this project but the source
was used to reverse engineer the API contracts.  https://github.com/twofas and https://2fas.com/
