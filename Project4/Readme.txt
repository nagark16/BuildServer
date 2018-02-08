This project by default set few values to GUI for display and trigger build process in backend, for demonstration. 

By default 2 child builders will be started and 3 build requests are sent.

Understanding GUI:
	1. In GUI author name should be entered in Author text box.
	2. To get list of folders on repository we have to click "Top" button.
	3. Doing this action will get list of all top directories in repository.
	4. By clicking on any of the folders we get list of all files under that folder.
	5. If we want to add those files to build request we have to select all the files and click on "Add" button.
	6. Then we can select multiple other projects in this way.
	7. After we have selected all required projects, we can click on "Generate XML" button to generate XML for the selected files.
	8. This will only save the generated XML in repository by copying via communication channel.
	9. In order run the XML we have to select the particular build request file under "buildRequests" folder and click on "Run" button.
	10. "Quit" button will close all opened processes via System.close()

Folder stucture in repository(displayed on screen too)
	1. "Logs" folder will have two subfolders. One for build logs and one for test harness logs.
	2. "BuildRequests" folder will contain all generated build request XMLs.
	3. Other folders contain source code.

NOTE: 
	1. For demonstration I am running 3 build requests on 2 Child Process. This process will take time.
	2. Can't close any process by closing window and try to run something. We have to close via "QUIT" button given in GUI.