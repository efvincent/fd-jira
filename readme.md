# FD Jira
A CLI/TUI tool/utility for manipulating Jira, specifically the way we use it on our team at Jet.com

## Why?
Started out as something else whatever, now it's an experiment in manipulating the Jira API and an excuse to play
with some CLI/TUI code, using a library called (Fons)[https://github.com/efvincent/fons] that I'm also working on.

## Jira API
This is a typical rest call:
```
 https://jira.walmart.com/rest/api/2/issue/RCTFD-4223\?fields\=changelog\&expand\=changelog
```

## Publishing .NET Core 3.0 single file
As of Core 3.0, dotnet can finally build into a single file. Settings in the fsproj:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishSingleFile>true</PublishSingleFile>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```
Note the runtime indentifier needs to be specified, either in the `fsproj` file above, 
or leave it out and use the cli switch below. The `PublishedTrimmed` flag does tree shaking.
```bash
$ dotnet publish -r osx-x64 -c release /p:PublishSingleFile=true /p:PublishTrimmed=true
```
For this project and anything `netstandard2.0` or better, you can use `win-x64` and `osx-x64`. 

For linux a bit more complicated:

* `linux-x64` (Most desktop distributions like CentOS, Debian, Fedora, Ubuntu and derivatives)
* `linux-musl-x64` (Lightweight distributions using [musl](https://wiki.musl-libc.org/projects-using-musl.html) like Alpine Linux)
* `linux-arm` (Linux distributions running on ARM like Raspberry Pi)


## Some links
* [Jira API v2 Documentation](https://developer.atlassian.com/cloud/jira/platform/rest/v2)
* [Scott Hanselman's blog on single file .NET](https://www.hanselman.com/blog/MakingATinyNETCore30EntirelySelfcontainedSingleExecutable.aspx)
* [.NET Core Runtime Info](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)
* [LiteDB](https://www.litedb.org/) - embedded doc store, .NET and F# friendly
* [F# client for LiteDB](https://github.com/Zaid-Ajaj/LiteDB.FSharp)
* [Fons CLI lib for F#](https://github.com/efvincent/fons)
