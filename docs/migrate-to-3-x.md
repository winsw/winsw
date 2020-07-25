# Migrate to 3.x

## Automatic migration

TODO

## Manual migration

1. Remove `<stopparentprocessfirst>`.
1. Merge `<domain>DomainName</domain>` and `<user>UserName</user>` into `<username>DomainName\UserName</username>`. If the user account belongs to the built-in domain, you can specify `<username>.\UserName</username>`.
   - Consider removing `<password>` from config file and using `<prompt>` in interactive context.
1. Remove `<waithint>` and `<sleeptime>`.
