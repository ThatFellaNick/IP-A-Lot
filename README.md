# IP-A-Lot
Find a lot of IPs with this light-weight IP scanner!

IP A Lot is a small .NET desktop scanner for quick LAN inventory work. It accepts
multiple IPv4 range formats, prefills detected local networks, and shows online
hosts with MAC addresses plus vendor lookup when the OUI is known.

## Range examples

- `192.168.1.0/24`
- `192.168.2.0-254`
- `10.0.0.20-10.0.0.50`
- Multiple ranges separated by commas, semicolons, or new lines

## Build

Requirements for running:

- Windows machine
- .NET Framework 4.x runtime, already present on many supported Windows installs

Requirements for building:

- .NET SDK with .NET Framework 4.8 targeting support

Build the small framework-dependent executable with:

```powershell
dotnet build .\IPALot.sln -c Release
```

The executable is created at:

```text
src\IPALot\bin\Release\net48\IP A Lot.exe
```

## OUI vendor data

The app includes a compact built-in vendor list. To extend it, place an `oui.csv`
file next to the executable with rows like:

```csv
001122,Example Vendor
AA-BB-CC,Another Vendor
```
