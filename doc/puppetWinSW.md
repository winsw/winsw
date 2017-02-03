## Puppet Module

WinSW can be managed using Puppet.

Please checkout the [Puppet Forge Page](http://forge.puppet.com/kenmaglio/winsw) for more information about specifics of this module. 

# winsw

### Table of Contents

1. [Important](#important)
1. [Description](#description)
1. [Setup - The basics of getting started with winsw](#setup)
    * [Beginning with winsw](#beginning-with-winsw)
1. [Usage - Configuration options and additional functionality](#usage)
    * [Additional Configurations](#additional-configuration-parameters)
1. [Reference - An under-the-hood peek at what the module is doing and how](#reference)
1. [Limitations - OS compatibility, etc.](#limitations)
1. [Development - Guide for contributing to the module](#development)

## Important

If you change the $service_id value, after you have installed the service, and you do not ensure abscent first, you will cause errors.
The reason is because the code which tried to uninstall, will already have been effected.

Tested on Windows 10 and Windows Server 2012 R2.
There shouldn't be any reason this wouldn't work on 

## Description

This module encapsulates functionality of the WinSW service application wrapper.
The development of that project is accredited: [https://github.com/kohsuke/winsw](https://github.com/kohsuke/winsw)

This module attempts to allow any executable with any arguments to be wrapped in a Windows Service.
This will require files to be placed on the system in a managed path: EXE, XML, EXE.Config

### Derived Types:
* install 
* service

Install will create the folders in $install_path, drop the files in that folder named $serviceid[.exe|.xml].
Then after those are successfull, the defined type will install the service into the Service Manager.

Service will ensure the service is running.

### Beginning with winsw

By default, classifying a node with this class will not get you very far.
It will test that the module will work and will run an instance of powershell.exe as a service.

You can take two approaches:

1. Use the Defined Types under Usage in your own module. They will automatically be created once you add this module ot your puppet file.
   1. You can build your own class that manages multiple services this way, if you so choose.
1. You can classify a node with the winsw class, and use hiera to override the local variables.

## Usage

Usage Pattern for Installing and Configuring
Title = name of executable / service

<pre><code>
  winsw::install { 'MyService':
    ensure                  => present,
    service_name            => $service_name,
    service_executable      => $service_executable,
    service_argument_string => $service_argument_string,
  } ->
  winsw::service { 'MyService':
    ensure => running,
  }
</code></pre>

Optional Parameters
<pre><code>
    winsw_binary_version    => $winsw_binary_version,
    install_path            => $install_path,
    service_description     => $service_description,
    service_env_variables   => $service_env_variables,
    service_logmode         => $service_logmode,
</code></pre>

Usage Pattern for Uninstalling
<pre><code>
  winsw::install { 'MyService':
    ensure => absent,
  }
</code></pre>

### Additional Configuration Parameters


To Specify Service Account to run service as
<pre><code>
    service_user            => 'your_serviceaccount',
    service_pass            => 'your_serviceaccount_password',
    service_domain          => 'your_serviceaccount_domain'
</code></pre>

To Run Interactively (not service account cannot be used - only local system)
<pre><code>
    service_interactive     => $true
</code></pre>

## Reference

The module includes embedded the winsw executable file, and provides a template for the configuration XML. 
It attepts to create whatever directories you need specified by $install_path
Then drops the needed files in that path as $service_name(.exe|.xml)

Utilizing exec's against powershell this module will then manage the behavior flow of winsw commands.

## Known Side-Effects

On initial install, the output will show not only the Exec[install_serviceid], but also the Exec[rebuild_service_serviceid].
This is expected as the config xml file is placed, which fires the notify on Exec[rebuild_service_serviceid].
This notify is needed if a config xml file change happens. The service must be stopped, uninstalled, installed and started to take effect.


## Limitations

Limitations for current release are really more around parameters which the native WinSW executable can take, which have not been implemented here yet.
Right now only the basics to get an executable running, with arguments and environment variables are possible.

More will be added in later revisions.

If you need one specifically please open an issue here on github, and I will try to add that functionality quickly for you.

See: [https://github.com/kohsuke/winsw](https://github.com/kohsuke/winsw)

## Development

#### Please fork and submit pull requests

To setup local environment:
<pre><code>
puppet module install puppetlabs-powershell --version 2.1.0 --modulepath=[your path to modules here]
puppet apply -v -e 'include winsw' --modulepath=[your path to modules here]
</code></pre>
You can include --noop if you don't want to apply, however service actions will fail as it won't actually install.

If you run an elevated command prompt, you can navigate to the service executable directory.
Then you can use these to test states of your service and the module. (note MyService is your servie name)
<pre><code>
MyService.exe stop
MyService.exe uninstall
MyService.exe start
MyService.exe install
</code></pre>
