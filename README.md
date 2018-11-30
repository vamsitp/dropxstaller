# dropxstaller
> Download and Install UWP Packages from Azure DevOps Drop location (zip)

### Steps:
1. Verify that you have Admin access on the PC
2. Goto PC Settings > Update & Security > For Developers > Select 'Developer Mode' under 'Use developer features'
3. From Elevated PowerShell window run this command: Set-ExecutionPolicy RemoteSigned 
   Select [Yes to All] - A
4. Generate a Personal Access Token at https://account.visualstudio.com/_details/security/tokens
   Select 'All Scopes' option
5. Update dropxstaller.exe.config with the generated/copied Token
6. Double-click dropxstaller.exe

### Usage: By default, the application downloads the latest Drop package and installs it
1. If you have already downloaded the package, run the application from a CMD window with -i option to just extract and install the downloaded package/zip file
2. If you want to download a specific package, provide the Build/Container ID as a value for BuildOrContainerId in dropxstaller.exe.config
   [List of Containers can be found at: https://account.visualstudio.com/_apis/resources/Containers]

