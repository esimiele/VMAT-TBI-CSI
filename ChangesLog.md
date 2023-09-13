# VMAT TBI and CSI autoplanning code

## updates
update 4/14/23
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

###beam placement 
- Contour overlap feature now works
- optimization constraints are automatically to the appropriate plan for added field junctions
- enhanced logging of errors

### optimization setup
- minimal changes (mainly removing unused pieces of code)

### template building
- updated logic to include ring generation from the UI

### optimization
- updated search for log file from prep script (primarily updated which directories are searched to match the updated logic in the prep script)

update 3/28/23
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

update 1/10/2023
- introduced preliminary algorithm to contour the arms to avoid in optimizer
- updated isocenter placement algorithm
- update ring placement algorithm
- ran single test case (by hard-coding necessary changes in executable)

update 1/3/2023
- fixed issue where VMAT TBI option would result in a 'cannot open patient' error popping up
- fixed issue where the optimization constaint assignment success message would display even if nothing was assigned
- fixed VMAT CSI iso naming (brain, upspine, lowspine)
- changed button content from 'add target' to 'create target'
- added logic for scan SS and create targets to exclude any structure with 'ts_' in the Id