WinSW Developer Information
===

### Build Environment

* IDE: [Visual Studio Community 2019](https://visualstudio.microsoft.com/vs/) (free for open-source projects)
* *winsw_key.snk* should be available in the project's root in order to build the executable
* You can generate the certificate by running *generate-key.ps1*
* The certificate is in *.gitignore* list. Please do not add it to the repository

### Testing

WinSW includes a set of tests powered by the [NUnit](https://www.nunit.org/) test framework.
In order to run tests you can install [NUnit Console](https://github.com/nunit/nunit-console) on the build machine.

Once you build the solution, you will be able to find the test DLL with dependencies in the `src/Test/winswTests/bin` directory.
In NUnit Console you can just import projects from this directory and then run tests.
 
### Continuous Integration

Project has a continuous integration flow being hosted by AppVeyor ([project page](https://ci.appveyor.com/project/oleg-nenashev/winsw)).
This CI instance automates building and testing of the Release configuration of WinSW. 
See [the appveyor.yml file](./appveyor.yml) for more details.

Current status: [![Build status](https://ci.appveyor.com/api/projects/status/i94752yal9iy77in?svg=true)](https://ci.appveyor.com/project/oleg-nenashev/winsw)

### Releasing to GitHub and NuGet

Releases are being performed to 3 locations: GitHub, NuGet, and Jenkins Maven Repository.
For all these releases we use binaries being created by the special AppVeyor Job ([winsw-release](https://ci.appveyor.com/project/oleg-nenashev/winsw-release)).

Here are the release steps:

1. Integrate all pull requests you want to release to the master branch.
2. Update [CHANGELOG](./CHANGELOG.md) and push changes to the master.
3. Wait till the [AppVeyor build](https://ci.appveyor.com/project/oleg-nenashev/winsw) finishes for the last commit.
4. Go to the [winsw-release job page](https://ci.appveyor.com/project/oleg-nenashev/winsw-g2fwp).
5. If you are doing a release with a new feature, bump the second digit in the _Version_ setting (e.g. to `2.N.${build}`) and change the next build number to `0`. In such case the version in assembly info will be `2.N.0`
6. Run the [winsw-release](https://ci.appveyor.com/project/oleg-nenashev/winsw-g2fwp) build. 
Once it completes, ensure the version is correct.
7. Click on the _Deploy_ button for the build.
Then deploy changes to _GitHub Releases_ and NuGet using the available publishers.
8. Go to [GitHub Releases](https://github.com/kohsuke/winsw/releases), find the published Release, click on _Edit release_ and then uncheck the _This is a pre-release_ checkbox to make the release public.


### Releasing to the Maven repository (legacy)

Maven repository is no longer the main source of releases,
but some projects still depend on WinSW from the Jenkins Maven repository.
Maven Packaging repository can be found [here](https://github.com/jenkinsci/winsw-maven-packaging).
