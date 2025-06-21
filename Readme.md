# VMAT TBI and CSI Auto Planning

An automated planning solution for VMAT TBI and VMAT CSI using the Eclipse Scripting Application Programming Interface (ESAPI)

## Authors
- Primary contributors and authors:
	- Eric Simiele: primary developer and maintainer of code
	- Ignacio Romero: developer and tester
	- Nataliya Kovalchuk: tester and project PI
	- Jen-Yeu Wang: DICOM import/export developer and AI autosegmentation model developer

## Updates
See [Changes log](https://github.com/esimiele/VMAT-TBI-CSI/blob/master/ChangesLog.md)


## Initial public release of code v1.0
- The purpose of this code is to automate as much of the treatment planning process as possible for VMAT TBI and CSI following the planning techniques used at Stanford University
- Code is now available to public following clinical release at Stanford
- Feel free to download and use the code
- No example patients are provided with the code
	- Wanted to avoid dealing with any privacy/HIPAA issues
- The solution file is located under VMATTBICSIAutoPlanMT/VMATTBICSIAutoPlanMT.sln
- This code has been developed over the past year through a Stanford Clinical Innovation Fund grant
- This code utilized the initial framework from the [VMAT TBI autoplanning code](https://github.com/esimiele/VMAT-TBI), but has been massively overhauled and expanded to permit VMAT CSI autoplanning
	- In addition, it has been rewritten to be more general, maintainable and readable
	- All of the code has been refactored to try and limit the maximum line count in any one function to 100 lines and make the functions into much smaller units (compared to the VMAT TBI autoplanning code)
		- permits unit testing
- Much of the code has been abstracted into the VMATTBICSIAutoPlanningHelpers library, which contains many useful methods that are relevant to both VMAT TBI and CSI autoplanning

## Install and run guide
### Install and build
- Clone the repository to your local computer
- Open the solution file in visual studio
- Fixes any references to the Varian DLL files for the following projects:
	- ImportListener
	- VMATCSIAutoPlanMT
	- VMATTBIAutoPlanMT
	- VMATTBICSIAutoplanningHelpers
	- VMATTBICSIOptLoopMT
- The code was compiled using the v15.6 ESAPI libraries
	- v16 libraries are included with the code
- Ignore any warnings from ParallelTest and VMATTBICSIAutoplanningHelpersTests
	- ParallelTest was a test project at parallelizing the optimization/dose calculation for sequential CSI plans (haven't decided what I want to do with it yet)
	- VMATTBICSIAutoplanningHelpersTests are the unit tests for VMATTBICSIAutoplanningHelpers (still updating them)
- In the solution explorer, under the \_configuration folder, open the log_configuration.ini file in visual studio and update the log file path to a location on your computer
	- specifically, a drive that can be seen and written to by citrix
- Under the VMATCSIAutoPlanMT and VMATTBIAutoPlanMT projects, in the Configuration folders, open the .ini configuration files and update the file paths to the desired locations on the computer/network
	- documentation
	- image export
	- Aria DB daemon, VMS File Daemon, local Daemon information (if you want automated import/export of CT and RT Struct dicom data)
	- RT structure set import file location
- At the top of Visual Studio select --> build --> rebuild solution
- Resolve any build errors

### Troubleshooting
- Again, don't care about build failures for ParallelTest and VMATTBICSIAutoplanningHelpersTests
- A likely failure is the varian ESAPI dlls not being found/correctly referenced
	- be sure to update them to **YOUR VERSION OF ECLIPSE/ESAPI** for all projects listed in the installation section
- Another failure is in nuget package restoration. This project uses two main nuget packages: simpleprogresswindow and EvilDicom
- Info on the two packages can be found here:
	- Github: [SimpleProgressWindow](https://github.com/esimiele/SimpleProgressWindow) Nuget: [SimpleProgressWindow](https://www.codecademy.com/resources/docs/markdown/links)
	- [EvilDicom](https://github.com/rexcardan/Evil-DICOM) 
- Only VMATTBICSIAutoplanningHelpers project uses EvilDicom
- VMATTBICSIAutoplanningHelpers, VMATCSIAutoPlanMT, and VMATTBIAutoPlanMT all use simpleprogresswindow
- To update the nuget packages, right click on the project --> manage nuget packages
	- If the package manager shows the packages as installed and working fine, uninstall both packages and reinstall them
		- after uninstalling, select browse and search for simpleprogresswindow and evildicom and install them to the above projects
- Try rebuilding the solution now, everything should work

### Build files and testing
- All files will be built in the top parent directory under /bin
- Included in this folder are the configuration files (placed in a created folder under /bin/configuration) and plan template files (/bin/templates/\<plan type\>)
	- Both autoplanning scripts have been built around the concept of plan templates
	- These plan template files are read upon launch and are available to the user for selection for planning
		- They contain the relevant information regarding targets, rings, optimization structures, optimization constraints, etc.
- Upon successful build of all projects, at the top of Visual Studio next to the build configuration drop downs, select the VMATCSIAutoPlanMT project in the drop down (i.e., which project to launch in debug mode)
- Hit Start, there should be one error message regarding no connection to aria. Pay attention to any other error messages, particularly any messages regarding not being able to find folders
- Once the gui pops up, switch to the Script configuration tab and review the settings to ensure they match what you changed in the .ini files previously

### Run
- The scripts can be run either through citrix or as stand-alone applications on a thick-client
- To run through citrix:
	- Copy the bin/ folder and all of its contents to a network drive that citrix can access
	- Open a patient structure set in Eclipse, select tools --> scripts --> change folder --> select folder --> navigate to the bin/ directory --> hit ok
		- A script should show up in the scripts window called *LaunchVMATTBICSIAutoPlan.cs* select this script and hit run
		- You will be presented with a small window asking you to choose between VMAT TBI and VMAT CSI, select VMAT CSI
		- The VMAT CSI UI should pop up. Repeat for VMAT TBI to ensure everything is wired up correctly
- To run as a stand-alone application:
	- Copy the bin/ folder and all of its contents to the desktop of a thick client
		- Open the bin/ folder and double click on the VMATCSIAutoPlanMT.exe file
			- You will be prompted to enter a patient MRN and a warning message should pop up saying that you need to select a structure set in the UI
		- Do the same for the VMATTBIAutoPlanMT.exe file

### Approvals
- Please test the code on a t-box prior to approving in your clinical system
	- **I'm not responsible if the code is not configured correctly for your system and it ends up causing problems**
- Once you have configured the code correctly and tested it and are ready to move to the clinical system, you will need to approve the following files under script approvals:
	- VMATCSIAutoPlanMT
	- VMATTBIAutoPlanMT
	- VMATTBICSIAutoplanningHelpers
	- VMATTBICSIOptLoopMT
	- ImportListener

## Contributing
- The authors welcome contributions, suggestions, issues, etc.
- For contributions, fork the code, make your changes and open a pull request with a short description of your changes
	- I will review it and determine if it should be incorporated into the code
- For all other items:
	- Feel free to open an issue for problems with the code or feature requests
	- I monitor it fairly regularly so I should get back to you in a week or so
