// CreatureEvolution.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <stdlib.h>
#include <Windows.h>
#include <processthreadsapi.h>
#include <iostream>
#include <string>
 
#include <stdio.h>
#include <conio.h>
#include <tchar.h>

int main(int argc, char* argv[])
{
	/*
	Set to true to test this on an executable.
	Set to false to test this on an in-editor version of the program.
	*/
	bool runExecutableInstance = false;

	STARTUPINFO startupInfo; // [in]
	ZeroMemory(&startupInfo, sizeof(startupInfo)); // Clear memory for this in parameter.
	PROCESS_INFORMATION processInformation; // [out] No need to clear memory. Will be set by function call.
	startupInfo.cb = sizeof startupInfo; // Provide the size of the startupinfo (multiple types and sizes).

	std::string pipename = "Pipe";
	std::string pipepath_str = "\\\\.\\pipe\\" + pipename;
	LPCSTR pipepath = LPCSTR(pipepath_str.c_str());
	HANDLE hPipe;
	BOOL flg;
	DWORD dwWrite, dwRead;
	char szServerUpdate[200];
	char szClientUpdate[200];
	
	hPipe = CreateNamedPipeA(pipepath,
		PIPE_ACCESS_DUPLEX,
		PIPE_TYPE_MESSAGE |
		PIPE_READMODE_MESSAGE |
		PIPE_WAIT,                    //changed from nowait
		PIPE_UNLIMITED_INSTANCES,    // max. instances 
		512,                    // output buffer size 
		512,                    // input buffer size 
		NMPWAIT_USE_DEFAULT_WAIT,                // client time-out 
		NULL);                        // no security attribute 

	if (hPipe == INVALID_HANDLE_VALUE)
	{
		std::cout << "Failed to create named pipe: " << GetLastError();
		return 1;
	}


	if (runExecutableInstance)
	{
		std::cout << "Creating process...\n";
		// Create a process for simulating a creature.
		// TODO: read args and see if we're running in batchmode or visualizing a simulation of an existing creature
		const std::string executable = "CreatureSimulation\\Builds\\08-27-2023_21-46\\CreatureSimulation";
		const std::string executable_args =
			"-batchmode " 
			"-nographics "
			"-logFile \"output.log\" "
			"-p " + pipename;
		std::string cmdLine_str = executable + " " + executable_args;
		BOOL success = CreateProcessA(
			NULL,
			LPSTR(cmdLine_str.c_str()),
			NULL,
			NULL,
			FALSE,
			NORMAL_PRIORITY_CLASS,
			NULL,
			NULL,
			LPSTARTUPINFOA(& startupInfo),
			&processInformation
		);

		if (!success)
		{
			std::cout << "Failed to create process: " << GetLastError();
			return 1;
		}

		std::cout << "Finished creating process.\n";
	}

	ConnectNamedPipe(hPipe, NULL);

	std::cout << "Process connected to pipe.\n";

	// send data to subprocess

	char to_write[200] = "{\n\"XScale\":1.0,\"YScale\":1.0,\"ZScale\":0.05,\"XRot\":0,\"YRot\":45,\"ZRot\":0}\n";
	WriteFile(hPipe, to_write, strlen(to_write), &dwWrite, NULL);

	// Wait for simulation to finish execution.
	WaitForSingleObject(processInformation.hProcess, INFINITE);
	// Cleanup the simulation process.
	CloseHandle(processInformation.hThread);
	CloseHandle(processInformation.hProcess);

	std::cout << "Process finished executing.\nContents:\n";

	// Get data from subprocess
	char buffer[200];
	DWORD bytesRead;

	if (ReadFile(hPipe, buffer, sizeof(buffer), &bytesRead, NULL)) {
		if (bytesRead > 0) {
			buffer[bytesRead] = '\0';
			std::cout << buffer << "\n";
		}
	}
	else
		std::cout << "No Contents\n";

	DisconnectNamedPipe(hPipe);

	std::cout << "Closing.";

	return 0;
}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
