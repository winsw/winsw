# CLI commands

## `install` command

Installs the service.

### Usage

```
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

```
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

```
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

```
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

```
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

```
winsw status [<path-to-config>]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

## `test` command

Checks if the service can be started and then stopped without installation.

### Usage

```
winsw test [<path-to-config>] [--no-elevate] [--timeout <timeout>] [--no-break]
```

### Arguments

`path-to-config`

The path to the configuration file.
If a file isn't specified, WinSW searches the executable directory for a *.xml* file with the same file name without the extension.

### Options

- `--no-elevate`

  Doesn't automatically trigger a UAC prompt.

- `--timeout <timeout>`

  Specifies the number of seconds to wait before the service is stopped.
  If not specified or -1 is specified, WinSW waits for a keystroke indefinitely.

- `--no-break`

  Ignores keystrokes.
  If specified, WinSW waits for Ctrl+C.
