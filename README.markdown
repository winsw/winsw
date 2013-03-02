winsw: Windows service wrapper in less restrictive license
=========================

Why?
----
Now, I think the first question that people would ask is, why another, when there's [Java Service Wrapper project](http://wrapper.tanukisoftware.org/doc/english/download.jsp) already available. The main reason for writing my own was the license — Java Service Wrapper project is in GPL (so that they can sell their commercial version in a different license), and that made it difficult for [Jenkins](http://jenkins-ci.org/) (which is under the MIT license) to use it.

Functionality-wise, there's really not much that's worth noting; the problem of wrapping a process as a Windows service is so well defined that there aren't really any room for substantial innovation. You basically write a configuration file specifying how you'd like your process to be launched, and we provide programmatic means to install/uninstall/start/stop services. Another notable different is that winsw can host any executable, whereas Java Service Wrapper can only host Java apps. Whether you like this or not depends on your taste, so I wouldn't claim mine is better. It's just different.

As the name implies, this is for Windows only. Unix systems have their own conventions for daemons, so a good behaving Unix daemon should just be using launchd/upstart/SMF/etc, instead of custom service wrapper.

Usage
-----
You write the configuration file that defines your service. This is the one I use for Jenkins:

    <service>
      <id>jenkins</id>
      <name>Jenkins</name>
      <description>This service runs Jenkins continuous integration system.</description>
      <env name="JENKINS_HOME" value="%BASE%"/>
      <executable>java</executable>
      <arguments>-Xrs -Xmx256m -jar "%BASE%\jenkins.war" --httpPort=8080</arguments>
      <logmode>rotate</logmode>
    </service>

You'll rename `winsw.exe` into something like `jenkins.exe`, then you put this XML file as `jenkins.xml`. The executable locates the configuration file via this file name convention. You can then install the service like:

    jenkins.exe install

... and you can use the exit code from these processes to determine whether the operation was successful. There are other commands to perform other operations, like `uninstall`, `start`, `stop`, and so on.

Available commands
------------------
Your renamed `winsw.exe` accepts the following commands:

* `install` to install the service to Windows Service Controller
* `uninstall` to uninstall the service. The opposite operation of above.
* `start` to start the service. The service must have already been installed.
* `stop` to stop the service.
* `restart` to restart the service. If the service is not currently running, this command acts like `start`.
* `status` to check the current status of the service. This command prints one line to the console. `NonExistent` to indicate the service is not currently installed, `Started` to indicate the service is currently running, and `Stopped` to indicate that the service is installed but not currently running.


Error reporting
---------------
Winsw uses WMI underneath, and as such it uses its error code as the exit code. See <a href="http://msdn.microsoft.com/en-us/library/aa389390(VS.85).aspx">MSDN article</a> for the complete list of exit code.

When winsw is running as a service, more detailed error information is reported to the Windows event log.

Deferred file operations
------------------------
To support self updating services, winsw offers a mechanism to perform file operations before a service starts up. This is often necessary because Windows prevents files from overwritten while it's in use.

To perform file operations, write a text file (in the UTF-8 encoding) at `myapp.copies` (that is, it's in the same directory as `myapp.xml` and `myapp.exe` but with a different file extension), and for each operation add one line:

* To move a file, write "src>dst". If the 'dst' file already exists it will be overwritten.

The success or failure of these operations will be recorded in the event log.
