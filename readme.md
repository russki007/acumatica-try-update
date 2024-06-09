# try-update

This is a simple tool that will help in migrating Acumatica customization projects to 24R1. Specifally, update all of existing DAC declarations by extending the `PXBqlTable` class.

```
public class MyDAC : PXBqlTable, IBqlTable { }
```

## How To Use The Tool
Nagivate to the root of your solution and simply execute:
```
try-update
```

```
try-update C:\src\sol.sln --apply-changes -r C:\src\CodeFix 
```

```
try-update C:\src\pro.csproj --apply-changes -r C:\src\CodeFix 
```


If you want more help from the tool, run:
```
try-update -h
```

### How To Build From


You can build and package the tool using the following commands. The instructions assume that you are in the root of the repository.

```console
dotnet build
dotnet pack
```

The try-update.1.0.1.nupkg file is created in the folder identified by the `<PackageOutputPath>` value from the try-update.csproj file, which in this example is the ./artifacts folder.

### Install Tool
Install the tool from the package by running the dotnet tool install command in the root of project folder:

```
dotnet tool install --add-source .\artifacts\ -g try-update
dotnet try-update
```

> Note: On macOS and Linux, `.\artifacts` will need be switched to `./artifacts` to accommodate for the different slash directions.

### How To Uninstall

You can uninstall the tool using the following command.

```console
dotnet tool uninstall -g  try-update
```