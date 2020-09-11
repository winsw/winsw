<!-- NOTE: Keep descriptions in sync with codes. -->

# CLI commands

- [install](#install-command)
- [uninstall](#uninstall-command)
- [start](#start-command)
- [stop](#stop-command)
- [restart](#restart-command)
- [status](#status-command)
- [refresh](#refresh-command)
- [customize](#customize-command)

## `install` command

Installs the service.

### Usage

```console
winsw install [<path-to-config>] [--no-elevate] [--user|--username <username>] [--pass|--password <password>]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

- `--user|--username <username>`

  Specifies the user name of the service account.

- `--pass|--password <password>`

  Specifies the password of the service account.

## `uninstall` command

Uninstalls the service.

### Usage

```console
winsw uninstall [<path-to-config>] [--no-elevate]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

## `start` command

Starts the service.

### Usage

```console
winsw start [<path-to-config>] [--no-elevate]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

## `stop` command

Stops the service.

### Usage

```console
winsw stop [<path-to-config>] [--no-elevate] [--no-wait]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

- `--no-wait`

  Doesn't wait for the service to actually stop.

- `--force`

  Stops the service even if it has started dependent services.

## `restart` command

Stops and then starts the service.

### Usage

```console
winsw restart [<path-to-config>] [--no-elevate]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

- `--force`

  Restarts the service even if it has started dependent services.

## `status` command

Checks the status of the service.

### Usage

```console
winsw status [<path-to-config>]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

## `refresh` command

Refreshes the service properties without reinstallation.

### Usage

```console
winsw refresh [<path-to-config>] [--no-elevate]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

## `customize` command

Customizes the wrapper executable.

### Usage

```console
winsw customize -o|--output <output> --manufacturer <manufacturer>
```

### Options

- `-o|--output <output>`

  Required. Specifies the path to the output file.

- `--manufacturer <manufacturer>`

  Specifies the manufacturer name of the customized executable.

## `dev ps` command

Draws the process tree associated with the service.

### Usage

```console
winsw dev ps [<path-to-config>] [-a|--all]
```

### Options

- `-a|--all`

  Optional. Draws the process tree associated with all services.

## `dev kill` command

Terminates the service if it has stopped responding.

### Usage

```console
winsw dev kill [<path-to-config>] [--no-elevate]
```

## `dev list` command

Lists services managed by the current executable.

### Usage

```console
winsw dev list
```
