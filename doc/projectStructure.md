# Project Structure

:movie_camera: [You can find code dive session recorded video here](https://youtu.be/_adhRj19ESY)

```
|_ doc
|_ eng
|_ examples
|_ src
    |_ Core
    |   |_ ServiceWrapper
    |   |_ WinSWCore
    |_ Plugins
    |_ Test/winswTest
```

## :open_file_folder: doc

This directory contains all the documents related to the WinSW project.

## :open_file_folder: eng

### :page_facing_up: build.yaml  

This contains Build configurations. We have another release pipeline that is not yet added to this repository and will be added in later.  

## :open_file_folder: examples

This folder contains templates for configuration files. Currently, there are XML configuration templates. YAML templates will be added in later.  

### :page_facing_up: sample-minimal.xml

This contains a template for mandatory configurations.

### :page_facing_up: sample-allOptions.xml

This template contains all possible configurations with documentation.

## :open_file_folder: src

This contains the implementation of the WinSW.

## :open_file_folder: Core

## :notebook: ServiceWrapper

This is the main executable project.
```
|_ ServiceWrapper
    |_ Logging
    |   |_ WrapperServiceEventLogProvider.cs
    |_ Properties
    |   |_ AssemblyInfo.cs
    |_ Main.cs
    |_ NullableAttributes.cs
    |_ winsw.csproj
```

#### :page_facing_up: Main.cs

This file contains the entry point of the program. (Main method). This file includes the main flow of the program and has implemented the logics for command-line arguments.  
ex : ```install, uninstall, start, stop, restart```

## :notebook: WinSWCore

```
|_ WinSWCore
    |_ Configuration
    |_ Extensions
    |_ Logging
    |_ Native
    |_ Util
    |_ Download.cs
    |_ DynamicProxy.cs
    |_ LogAppenders.cs
    |_ NullableAttributes.cs
    |_ PeriodicRollingCalendar.cs
    |_ ServiceDescriptor.cs
    |_ WinSWCore.csproj
    |_ WinSWException.cs
    |_ WinSWSystem.cs
    |_ Wmi.cs
    |_ WmiSchema.cs
```

#### :page_facing_up: ServiceDescriptor.cs

This contains the logics for extracting configurations from the XML file. ```ServiceDescriptor``` class get XML file as an argument. Currently, configurations are provided on demand.

## :open_file_folder: Configuration

#### :page_facing_up: IWinSWConfiguration.cs

```IWinSWConfigurations``` interface contains all configurations.

#### :page_facing_up: DefaultSettings.cs

This contains default values for all configurations which included in ```IWinSWConfiguration.cs```.
