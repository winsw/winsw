# WinSW extensions

Starting from WinSW 2.0, the wrapper provides an internal extension engine and several extensions.
These extensions allow to alter the behavior of the Windows service in order to setup the required service environment.

## Available extensions

* [Shared Directory Mapper](sharedDirectoryMapper.md) - Allows mapping shared drives before starting the executable
* [Runaway Process Killer](runawayProcessKiller.md) - Termination of processes started by the previous runs of WinSW

## Developer guide

In WinSW v2 the extension does not support inclusion of external extension DLLs.

### Adding external extensions

The only way to create an external extension is to create a new extension DLL and 
  then to merge this DLL into the executable using tools like `ILMerge`.
See the example in `src/Core/ServiceWrapper/winsw.csproj`.

Generic extension creation guideline:
* Extension DLL should reference the `WinSWCore` library.
* The extension should extend the `AbstractWinSWExtension` class.
* The extension then can override event handlers offered by the upper class.
* The extension should implement the configuration parsing from the `XmlNode` and `YamlExtensionConfiguration`.
* The extension should support disabling from the configuration file.

WinSW engine will automatically locate your extension using the class name in the [XML configuration file](../xmlConfigFile.md) or [YAML configuration file](../yamlConfigFile.md).
See configuration samples provided for the extensions in the core.
For extensions from external DLLs, the `className` field should also specify the assembly name. 
It can be done via fully qualified class name or just by the `${CLASS_NAME}, ${ASSEMBLY_NAME}` declaration.

Please note that in the current versions of WinSW v2 the binary compatibility of extension APIs **is not guaranteed**.
