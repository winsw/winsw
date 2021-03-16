# Migrate to 3.x

## Automatic migration

TODO

## Manual migration

1. Remove `<name>` if you don't want it.
1. Remove `<description>` if you don't want it.
1. Merge `<argument>` into `<arguments>`.
1. Merge `<startargument>` into `<startarguments>`.
1. Merge `<stopargument>` into `<stoparguments>`.
1. Remove `<stopparentprocessfirst>`.
1. Merge `<domain>DomainName</domain>` and `<user>UserName</user>` into `<username>DomainName\UserName</username>`. If the user account belongs to the built-in domain, you can specify `<username>.\UserName</username>`.
   - Consider removing `<username>` and `<password>` from config file and using `<prompt>` in interactive context, or `--username` and `--password` command-line options in non-interactive context.
1. Remove `<waithint>`.
1. Remove `<sleeptime>`.
1. Replace `<delayedAutoStart />` with `<delayedAutoStart>true</delayedAutoStart>`.
1. Replace `<interactive />` with `<interactive>true</interactive>`.
1. Replace `<beeponshutdown />` with `<beeponshutdown>true</beeponshutdown>`.
1. Remove the `RunawayProcessKiller` extension.
1. Move `<mapping>.<map>` to `<service>.<sharedDirectoryMapping>.<map>`. Remove the `SharedDirectoryMapper` extension.
