winsw: Windows service wrapper in less restrictive license
=========================

Why?
----
Now, I think the first question that people would ask is, why another, when there's [Java Service Wrapper project](http://wrapper.tanukisoftware.org/doc/english/download.jsp) already available. The main reason for writing my own was the license — Java Service Wrapper project is in GPL (so that they can sell their commercial version in a different license), and that made it difficult for [Jenkins](http://jenkins-ci.org/) (which is under the MIT license) to use it.

Functionality-wise, there's really not much that's worth noting; the problem of wrapping a process as a Windows service is so well defined that there aren't really any room for substantial innovation. You basically write a configuration file specifying how you'd like your process to be launched, and we provide programmatic means to install/uninstall/start/stop services. Another notable different is that winsw can host any executable, whereas Java Service Wrapper can only host Java apps. Whether you like this or not depends on your taste, so I wouldn't claim mine is better. It's just different.

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
