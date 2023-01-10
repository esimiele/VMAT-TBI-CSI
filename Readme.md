# VMAT TBI and CSI autoplanning code

## updates
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

## Still to do
todo:
- reconcile when differences between init bst in template and UI

