# Project structure

:movie_camera: You can find the code dive session recorded video [here](https://youtu.be/_adhRj19ESY).

```
|_ doc
|_ eng
|_ examples
|_ src
    |_ Core
    |   |_ ServiceWrapper
    |       |_ Main.cs
    |   |_ WinSWCore
    |       |_ ServiceDescriptor.cs
    |       |_ Configurations
    |       |_ Extensions
    |_ Plugins
    |_ Test/winswTest
```

## :open_file_folder: examples

This folder contains templates for configuration files. *sample-minimal.xml* contains a template for mandatory configurations and *sample-allOptions.xml* contains all possible configurations with documentation.

## :open_file_folder: Core

## :notebook: ServiceWrapper

This is the main executable project. This contains the main.cs which is the entry point of the project and contains some features for logging.

### :page_facing_up: Main.cs

This file contains the entry point of the program (Main method). This file includes the main flow of the program and has implemented the logics for command line arguments. You can find more details about command line arguments [here](../../README.md#usage).

## :notebook: WinSWCore

This is the main component of the project. This contains the most important logics of the project such as ServiceDescriptor.cs, Extension api, configurations, etc.

### :page_facing_up: ServiceDescriptor.cs

This contains the logics for extracting configurations from XML file. The `ServiceDescriptor` class gets XML file as an argument. Currently configuratinos are retrieved on demand.

## :open_file_folder: Configuration

This contains the interface for the configurations. The `IWinSWConfiguration.cs` interface contains all configurations and  `DefaultSettings.cs` contains default values for the configurations.
