https://www.bitvise.com/tunnelier-service: 
> It is possible to run a Windows program as a system service. The advantage of services is that they can be started at boot time independently of any logon session, and will continue to run as users log on and off of the machine.
> 
> Frequently users would like to run Bitvise SSH Client as a service so that its port forwarding features would come into effect as soon as the machine boots and remain active regardless of users logging on and off of the machine.
Bitvise SSH Client itself does not run as a service, but it can be encapsulated inside a program that enables this. A few such programs we're aware of are:
> * The srvany utility included with the Windows Server 2003 Resource Kit. Instructions.
> * FireDaemon. This may be easier to set up, and more powerful than srvany.
> * We have received suggestions for the Non-Sucking Service Manager by Iain Patterson.

winsw is more actively maintained than Non-Sucking Service Manager.

Example configuration file (from https://github.com/winsw/winsw/issues/128#issue-187036133) :

```xml
<service>
  <id>bitvise-ssh-client</id>
  <name>bitvise-ssh-client</name>
  <description>This service runs a bitvise ssh client.</description>
  <workingdirectory>C:\install\Bitvise-SSH-Client\private</workingdirectory>
  <executable>stnlc</executable>
  <argument>-profile=jenkins-slave-websrv01-dev.bscp </argument>
  <argument>-keypairFile=jenkins-slave-websrv01-dev.key</argument>
  <logmode>rotate</logmode>
  <logpath>E:\logs</logpath>
</service>
```
