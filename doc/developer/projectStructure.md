# Project Structure

:movie_camera: [You can find code dive session recorede video here](https://youtu.be/_adhRj19ESY)

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

This folder contains templates for configuration files. `sample-minimal.xml` contains a template for mandotory configurations and `sample-allOptions.xml` contains all possible configurations with documentation.

## :open_file_folder: Core

## :notebook: ServiceWrapper

This is the main executable project. This contains the main.cs which is the entry point of the projet and contains some features for logging.

### :page_facing_up: Main.cs

This file contains the entry point of the program (Main method). This file includes the main flow of the program and has implemented the logics for following command line arguments.  
 ```install, uninstall, start, stop, stopwait, restart, restart!, status, test, testwait, help, version```

## :notebook: WinSWCore

WinSW library is the main component of the project. This contains the most important logics of the project such as ServiceDescriptor.cs, Extension api and configurations etc.

### :page_facing_up: ServiceDescriptor.cs

This contains the logics for extracting configurations from XML file. ```ServiceDescriptor``` class get XML file as a argument. Currently configuratinos are provided on demand.

## :open_file_folder: Configuration

This contains the interface for the configurations. ```IWinSWConfiguration.cs``` interface contains all configurations and  ```DefaultSettings.cs``` contains default values for the configurations.
