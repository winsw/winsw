# Contributing

## Prerequisites

You need to install either of the followings to develop .NET.

- [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) with *.NET desktop development* workload, which includes .NET SDK.
- .NET SDK and your favorite code editor.
  - You can find .NET SDK installation instructions on the [Download .NET](https://dotnet.microsoft.com/download) page.
  - You can try [Visual Studio Code](https://code.visualstudio.com/Download), which is an open source and cross-platform editor.

## Developing in Visual Studio

You can open `src\winsw.sln` and then build and run tests from within Visual Studio.

## Developing with .NET SDK

### Building

```
dotnet build src\winsw.sln
```

### Testing

```
dotnet test src\Test\winswTests\winswTests.csproj
```

## Project Structure

You can find the project structure guideline [here](doc/developer/projectStructure.md).
