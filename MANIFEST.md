# Project manifest

Here is a cite from [Kohsuke Kawaguchi](https://github.com/kohsuke/), who is the original author of this project:

> Now, I think the first question that people would ask is, why another, when there's [Java Service Wrapper project](http://wrapper.tanukisoftware.org/doc/english/download.jsp) already available. 
The main reason for writing my own was the license — Java Service Wrapper project is in GPL (so that they can sell their commercial version in a different license), and that made it difficult for [Jenkins](http://jenkins-ci.org/) (which is under the MIT license) to use it.

> Functionality-wise, there's really not much that's worth noting; the problem of wrapping a process as a Windows service is so well defined that there aren't really any room for substantial innovation. 
You basically write a configuration file specifying how you'd like your process to be launched, and we provide programmatic means to install/uninstall/start/stop services. 
Another notable difference is that winsw can host any executable, whereas Java Service Wrapper can only host Java apps. 
Whether you like this or not depends on your taste, so I wouldn't claim mine is better. 
It's just different.

> As the name implies, this is for Windows only. 
Unix systems have their own conventions for daemons, so a good behaving Unix daemon should just be using `launchd/upstart/SMF/etc`, instead of custom service wrapper.
