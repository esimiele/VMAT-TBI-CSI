# VMAT TBI and CSI autoplanning code updates

## 6/26/2025
- per Nataliya, all TBI/CSI linac couches are IGRT --> no bars in/out
    - No critical code adjusted, pushing directly to master branch

## 6/23/2025
- added additional structure ids for revised spinning manny version (v2). 
    - No critical code needed to be changed, only add additional string ids when searching for spinning manny couch
    - pushed directly to master branch

## 6/20/2025
### Launcher
- Updated logic for launcher to explicitly specify which context items are being passed

### TBI
- Fixed issue with incorrect shift note
- Added configuration option to automatically calculate dose for separated plans during plan preparation. If set to false, the user will be present with the option to recalculate dose for all plans (if applicable)
- Fixed issue causing crashing during beam placement where plan Id was too long for leg plans (only occurred when updating the requested number of vmat isocenters)
- Fixed issue where beam numbering was incorrect for leg fields

### CSI
- Fixed issue with incorrect shift note
- Added configuration option to automatically calculate dose for separated plans during plan preparation. If set to false, the user will be present with the option to recalculate dose for all plans (if applicable)

### Optimization
- Significantly improved logic for patient/plan selection
    - especially in cases where the plan preparation log file is missing or there is more than one log file present (e.g., if patient requires both TBI and CSI treatments)
- Added dropdown menus that allow user to manually select plans and their associated normalization volumes in UI
- Fixed issue causing crash during heater/cooler generation where optimization constraints were trying to be added to plan(s) for structures that do not exist
- Fixed issue that caused optimization loop to stop where plan prescriptions are absent from log files
    - In such case, the plan normalization volumes will be used instead for heater/cooler generation
- Updated reporting information for plan normalization, normalization volume, and target normalization

### 3/24/25
- Applied hotfix for lower leg iso placement for TBI autoplanner (when matchline to lower body extent was greater than 60 cm, iso was being placed relative to image origin rather than upper leg iso position --> now fixed)

### 9/15/2024
- Migrated all Tuples in solution to dedicated classes for easier mantenance and debugging
- Added unit tests for majority of methods in helpers library
- Added ability to prompt the user with 'reminders' prior to starting the optimization loop (e.g., did you set avoid entry through this structure?)
- Added tab to directly launch the import listener console. This is used when dicom import using the import listener failed on the first try (due to limitations in EvilDicom)
- Changed default option of creating AP/PA plans for TBI. Now multiple plans are created instead placing all isocenters in a single plan
- General bug fixes
- All unit tests passing and code tested on 4 dry-run cases for both CSI and TBI. Performs as expected

## 9/25/23
### general
- Hot fix for VMAT CSI import logic where the log file path was not being set correctly when the default log file path should be used

## 9/24/23
### general
- Implemented TBI TT collision check
- Implemented custom course Id specification via configuration files for both TBI and CSI
- minor bug fixes

## 9/23/23
### general
- fix for documentation buttons (targeting wrong pdf files)
- changed structure of binaries per suggestion of crcrewso
- minor change to build events

## 4/14/23
### general
- Major refactoring of code to simplify and improve efficiency
- Major improvements in logging, particularly organization and error reporting
- All uses of the script will be logged in separate text files. Each patient will have their own folder
where the logs will be stored. All uses with unsaved changes are saved in a folder for that patient called 'unsaved'.
Only one log file will be placed in the same directory as the unsaved directory, which contains the logs from the prep script use
where the changed WERE saved to the database. It is this log file that the optimization script loads and uses for the optimization loop.
- Started migrating sections of code to separate projects to provide better separation and improve re-usability of code

### structure tuning
- ring generation is now controlled through a separate tab on the UI
- do not add rings to the create TS call in the configuration file (it will not work). Instead use a call to 'create ring'
- no need to explicitly add optimization constraints for rings, automatically handled by code for the appropriate plan
- enhanced logging of errors

### beam placement 
- Contour overlap feature now works
- optimization constraints are automatically to the appropriate plan for added field junctions
- enhanced logging of errors

### optimization setup
- minimal changes (mainly removing unused pieces of code)

### template building
- updated logic to include ring generation from the UI

### optimization
- updated search for log file from prep script (primarily updated which directories are searched to match the updated logic in the prep script)

## 3/28/23
### general
- introduced sequential optimization as a feature
- much of the parameters for sequential optimization are controlled through both the UI and through the configuration files
- some parameters are only accessible through the configuration files: e.g., crop and contour overlap structures
- refactored substantial amount of code to remove redundancy and improve efficiency

### structure tuning
- numerous bug fixes and improvements
- finalized algorithm to contour the arms as avoidance
- fixed issue with ring placement algorithm
- introduced crop and contour overlap structures that will be cropped and have their overlap with all targets contoured if they overlap with all targets
- currently, crop and contour overlap structures must be adjusted in the configuration files
- introduced checks to see if targets exist before going and recreating them
- enhanced logging system to capture all modifications

### beam placement
- refined beam and isocenter placement algorithm
- increased x1 and x2 margins when fitting to target structures to ensure BEV target projections will be contained within field for all angles (will be adjusted in the future)
- enhanced logging system to capture all modifications

### optimization setup
- numerous modifications to the logic of adding optimization constraints
- specifically dealing with the issues surrounding having more than one plan to keep track of

### optimization
- substantial changes and refactoring of the optimization loop code
- automatically reads in patient log files when launching the script
- gives user an option to adjust the plan objectives rather than having to do it in the configuration files
- deal with all backend issues surrounding sequential optimization
- substantial improvements in progress reporting and logging
- each run of the script now generates a new log file rather than appending to the original log
- improved progress window UI to provide more information
- currently the CSI optimization is built off the optimization logic used for VMAT TBI. This will be changed in the future to something more tailed to VMAT CSI
- revised and simplified plan evaulation logic
- introduced logic to build a plan sum from two plans

## 1/10/2023
- introduced preliminary algorithm to contour the arms to avoid in optimizer
- updated isocenter placement algorithm
- update ring placement algorithm
- ran single test case (by hard-coding necessary changes in executable)

## 1/3/2023
- fixed issue where VMAT TBI option would result in a 'cannot open patient' error popping up
- fixed issue where the optimization constaint assignment success message would display even if nothing was assigned
- fixed VMAT CSI iso naming (brain, upspine, lowspine)
- changed button content from 'add target' to 'create target'
- added logic for scan SS and create targets to exclude any structure with 'ts_' in the Id