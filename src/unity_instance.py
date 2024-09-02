"""
@author William Erignac
@version 2024-09-01

This script contains a set of classes that interface with Unity to be used as a reinforcement learning environment.
The Unity project must be set up with a set of classes of its own. For details, open the Unity project in
UnityRLEnvironment.
"""

import threading
from enum import Enum
import win32file, win32pipe, win32event, pywintypes
import subprocess
import warnings


class SimulationTaskType(Enum):
    """
    An enum representing the possible states of a Unity project set up to be a reinforcement learning environment.
    """
    IDLE = 0
    SIMULATING = 1
    SETTING = 2
    QUIT = -1


class SimulationTask:
    """
    An object introducing different ways of interacting with Unity depending on what state it's in.
    This class is inherited for each SimulationTaskType.
    The base class implements functions for basic communication.
    """
    def __init__(self, pipe_handle, overlap, **kwargs):
        self.pipe_handle = pipe_handle
        self.read_thread = threading.Thread(target=self._read_content)
        self.read_lock = threading.Lock()
        # Triggered when some data was read.
        self._read_block_thread_event = threading.Event()
        # Object to handle asynchronous events.
        self._overlap = overlap


        # Lines of the pipe read but not sent to user.
        # Warnings and errors are not included in here.
        self.read_buffer = []

        # Extra parameters send by simulation instance. e.g. no_timeout
        self.meta_args = kwargs

    def get_task_type(self) -> SimulationTaskType:
        return NotImplemented

    def _get_finished_pipe_reading(self):
        """
        Called internally to see whether we've reached an 'end state'
        for this task.

        Override in conjunction with _on_read_line_from_pipe.
        """
        return NotImplemented

    def _on_read_line_from_pipe(self, line: str):
        """
        Called on reading thread when a full line with "\r\n" is finished being read
        from the pipe.
        Returns whether the line should be added to the buffer.

        Usually overriden to include detecting when this task has been complete.
        """
        # Check for warnings and print them.
        if line.startswith("Warning:"):
            tabbed_line = line.replace('\n', '\n\t')
            warnings.warn(f"Got warning from simulator: \n\t{tabbed_line}")
            return False
        # Check for errors and raise.
        if line.startswith("Error:"):
            tabbed_line = line.replace('\n', '\n\t')
            raise Exception(f"Got error from simulator: {tabbed_line}")
        return True

    def _is_line_finished(self, line):
        """
        Static function for seeing if a line is complete.
        """
        return line.endswith("\r\n")

    def _read_content(self):
        """
        Function that runs on the read thread. Finishes when self._get_finished_pipe_reading()
        returns false.
        """

        # While we haven't determined that this task is done, keep reading.
        while not self._get_finished_pipe_reading():
            # Allow read_line to block.
            self._read_block_thread_event.clear()

            # TODO: Check state of pipe
            # Read from pipe
            # https://stackoverflow.com/questions/57833774/python-pywintypes-overlapped-offset-throws-overflowerror
            ret, read_message = win32file.ReadFile(self.pipe_handle, 1024, self._overlap)
            bytes_read = win32file.GetOverlappedResult(self.pipe_handle, self._overlap, True)
            read_message = bytes(read_message[:bytes_read])
            if len(read_message) == 0:
                continue

            read_lines = read_message.decode().split("\r\n")

            self.read_lock.acquire()

            # If the newest line in the read buffer didn't finish, append to it.
            if len(self.read_buffer) > 0:
                last_line = self.read_buffer[-1]
                if not self._is_line_finished(last_line):
                    combined = last_line + read_lines.pop(0)
                    combined_split = combined.split("\r\n")
                    self.read_buffer[-1] = combined_split[0]
                    if len(combined_split) > 1:
                        self._add_partial_line_to_buffer(combined_split[1])

            # For each partial line, end the last line and add the partial line to the read_buffer.
            # If the message read ends on a newline, read_lines will have an extra empty
            # string at the end, marking the last line sa being complete.
            for line in read_lines:
                self._add_partial_line_to_buffer(line)

            self.read_lock.release()
            # Signal that we just finished reading some lines.
            self._read_block_thread_event.set()

    def _add_partial_line_to_buffer(self, partial_line):
        # Set the last line as a complete line.
        if len(self.read_buffer) > 0:
            self.read_buffer[-1] += "\r\n"
            keep = self._on_read_line_from_pipe(self.read_buffer[-1][:-2])
            if not keep:
                self.read_buffer.pop(-1)
        # Add the new line if appropriate.
        if len(partial_line) > 0:
            if self._get_finished_pipe_reading():
                warnings.warn(f'Received partial line "{partial_line}", after task was complete. Not adding to buffer.')
            else:
                self.read_buffer.append(partial_line)

    def _start_read_thread(self):
        # Run the read thread if we haven't already.
        if not (self.read_thread.is_alive() or self._get_finished_pipe_reading()):
            self.read_thread.start()

    def read_line(self, timeout=10.0, wait_step=0.5):
        """
        Returns a line written by the simulator. Blocks.
        Returns None if the simulator has reported that it has finished.
        """

        self.read_lock.acquire()

        # If we've received the end, we're out of lines.
        if len(self.read_buffer) == 0 and self._get_finished_pipe_reading():
            self.read_lock.release()
            return None

        # Get the oldest complete line from the buffer if there is one.
        buffer_has_line = len(self.read_buffer) > 0 and self._is_line_finished(self.read_buffer[0])
        if buffer_has_line:
            out = self.read_buffer.pop(0)[:-2]
            self.read_lock.release()
            return out

        # Run the read thread if we haven't already.
        if not (self.read_thread.is_alive() or self._get_finished_pipe_reading()):
            self._start_read_thread()

        self.read_lock.release()
        wait_time = 0

        # Polling for next line. Need to poll in case event is triggered after
        # lock release but before this line.
        while True:
            self._read_block_thread_event.wait(wait_step)
            self.read_lock.acquire()
            received_line = len(self.read_buffer) > 0 and self._is_line_finished(self.read_buffer[0])
            missed_end = self._get_finished_pipe_reading()
            self.read_lock.release()
            if received_line or missed_end:
                break
            wait_time += wait_step
            if (wait_time > timeout) and (timeout >= 0) and not self.get_meta_arg("no_timeout"):
                raise Exception(f"Timeout for {timeout} seconds when reading line.")

        return self.read_line()

    def write_line(self, to_write):
        # TODO: Check state of pipe
        ret, length = win32file.WriteFile(self.pipe_handle, f"{to_write}\n".encode())
        # TODO: Check ret and length

    def flush(self):
        # TODO: Check state of pipe
        win32file.FlushFileBuffers(self.pipe_handle)

    def get_meta_arg(self, arg_name: str):
        if arg_name in self.meta_args:
            return self.meta_args[arg_name]
        return None


class IdleTask(SimulationTask):
    """
    A dummy class for when Unity isn't doing anything.
    """
    def get_task_type(self):
        return SimulationTaskType.IDLE


class ExperimentTask(SimulationTask):
    """
    The class for when Unity is running an experiment. Automatically checks for the message that an experiment has ended.
    Otherwise, includes methods for running experiments, and waiting for an acknowledgement that an experiment environment
    has been opened.
    """
    def __init__(self, pipe_handle, overlap, experiment_name, **kwargs):
        SimulationTask.__init__(self, pipe_handle, overlap, **kwargs)
        # Whether we've signaled the end to the list of simulation session initialization data.
        self.has_sent_end = False
        # Whether we've received the signal that all the simulation sessions have been run.
        self.has_received_end = False
        # Type of experiment to run. Disambiguated by Unity simulation settings.
        self.experiment_name = experiment_name

    def get_task_type(self):
        return SimulationTaskType.SIMULATING

    def on_has_sent_end(self):
        self.has_sent_end = True

    def get_has_sent_end(self):
        return self.has_sent_end

    def _on_read_line_from_pipe(self, line):
        if not SimulationTask._on_read_line_from_pipe(self, line):
            return False

        if line == "END":
            self.has_received_end = True
            return False

        return True

    def _get_finished_pipe_reading(self):
        return self.has_received_end

    def signal_run_experiment(self):
        self.write_line(f"run {self.experiment_name}")

    def wait_run_experiment_response(self):
        assert(self.read_line() == "SUCCESS")


class QuitTask(SimulationTask):
    """
    The class for when Unity is closing. Includes a method to wait for the quitting protocol to have finished.
    """
    def __init__(self, pipe_handle, overlap, **kwargs):
        SimulationTask.__init__(self, pipe_handle, overlap, **kwargs)
        self.has_received_quit = False

        self._start_read_thread()

    def _on_read_line_from_pipe(self, line: str):
        if not SimulationTask._on_read_line_from_pipe(self, line):
            return False

        if line == "QUIT":
            self.has_received_quit = True
            return False

        return True

    def _get_finished_pipe_reading(self):
        return self.has_received_quit

    def signal_quit(self):
        self.write_line("quit")
        self.flush()

    def wait_for_quit_response(self):
        assert(self.read_line() is None)


class UnityInstance:
    """
    A class that wraps an instance of Unity set up for performing reinforcement learning experiments.
    """
    def __init__(self, pipe_path_and_name:str, executable_args:dict=None, **kwargs):
        """
        pipe_and_path_name = name + path of pipe to use for communication
        executable_args = {simulator_path:str, simulator_args:list[str]}

        If executable_args is not None, a Unity build with be executed using the provided args.
        Otherwise, no executable will be run. Useful for when running directly in the Unity editor.
        """
        # From https://www.codeproject.com/Questions/5340484/How-to-send-back-data-through-Python-to-Csharp-thr
        self.pipe_handle = win32pipe.CreateNamedPipe(
            pipe_path_and_name,
            win32pipe.PIPE_ACCESS_DUPLEX | win32file.FILE_FLAG_OVERLAPPED,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536,
            0,
            None)

        self.simulation_exec = None
        if not (executable_args is None):
            # Run on a different thread. run waits until the process has finished.
            self.simulation_exec = subprocess.Popen([executable_args['simulator_path']] + executable_args['simulator_args'])

        self.overlap = pywintypes.OVERLAPPED()
        self.overlap.hEvent = win32event.CreateEvent(None, 0, 0, None)
        win32pipe.ConnectNamedPipe(self.pipe_handle, self.overlap)
        win32file.GetOverlappedResult(self.pipe_handle, self.overlap, True)

        # Extra arguments for controlling behaviour. e.g. no_timeout.
        self.meta_args = kwargs

        # Current (assumed) state of the simulator executable.
        self.task: SimulationTask = IdleTask(self.pipe_handle, self.overlap, **self.meta_args)

    def set_property(self, property_name, value):
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception("Cannot set properties whilst simulation is running.")

        # TODO: Write to pipe

    def run_experiment(self, experiment_name:str):
        """
        Open an environment corresponding to the given experiment name.
        """
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception(f"Cannot start another task while task {self.task.get_task_type()} is running.")

        self.task = ExperimentTask(self.pipe_handle, self.overlap, experiment_name, **self.meta_args)

        self.task.signal_run_experiment()
        self.task.wait_run_experiment_response()

    def send_session_initialization_data(self, session_init_data):
        """
        session_init_data = iterable list of json strings representing session initialization data.
        """
        if self.task.get_task_type() != SimulationTaskType.SIMULATING:
            raise Exception("Cannot simulate sessions before starting an experiment.")

        if self.task.get_has_sent_end():
            raise Exception("Cannot send sessions. Already sent END for this simulation.")

        if type(session_init_data) == str or not hasattr(session_init_data, "__iter__"):
            session_init_data = [session_init_data]

        for init_data in session_init_data:
            self.write_line(init_data)

        self.flush_pipe()

    def end_send_session_initialization_data(self):
        """
        Call once all initialization data has finished sending to start session simulation.
        """
        if self.task.get_task_type() != SimulationTaskType.SIMULATING:
            raise Exception("Cannot end simulation. No simulations have started.")

        if self.task.get_has_sent_end():
            raise Exception("Cannot send end. Already sent end for this simulation.")

        self.write_line("END")
        self.flush_pipe()
        self.task.on_has_sent_end()

    def quit(self):
        """
        Call to close the Unity instance and communication pipe.
        """
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception("Cannot set quit whilst simulation is running.")

        self.task = QuitTask(self.pipe_handle, self.overlap, **self.meta_args)
        self.task.signal_quit()
        self.task.wait_for_quit_response()
        self.task = None
        self.close_pipe()
        if (not (self.simulation_exec is None)) and (not (self.simulation_exec.poll() is None)):
            self.simulation_exec.terminate()

    def write_line(self, to_write):
        self.task.write_line(to_write)

    def flush_pipe(self):
        self.task.flush()

    def read_line(self):
        line = self.task.read_line()

        if line is None and self.task.get_task_type() == SimulationTaskType.SIMULATING:
            self.task = IdleTask(self.pipe_handle, self.overlap, **self.meta_args)

        return line

    def close_pipe(self):
        win32file.FlushFileBuffers(self.pipe_handle)
        win32pipe.DisconnectNamedPipe(self.pipe_handle)
        win32file.CloseHandle(self.pipe_handle)


if __name__ == "__main__":
    import main
    import json

    def create_and_run_instance(id):
        print(f"Creating Connection for {id}...")
        pipe_name = f'Pipe{id}'
        exec_args = dict()  # Set to None for debugging in-editor.
        exec_args['simulator_path'] = "..\\CreatureSimulation\\Builds\\03-07-2024_00-51\\CreatureSimulation.exe"
        exec_args['simulator_args'] = ["-batchmode", "-nographics", "-p", pipe_name]

        instance = UnityInstance('\\\\.\\pipe\\' + pipe_name, exec_args)

        for i in range(2):
            print(f"Running Experiment for {id}...")
            instance.run_experiment("falling_rectangular_prism")

            print(f"Sending Simulation Sessions for {id}...")

            for i in range(256):
                instance.send_session_initialization_data(json.dumps(main.RectPrism().serialize()))

            print(f"Sending End {id}...")
            instance.end_send_session_initialization_data()

            print(f"Reading Lines {id}...")
            creature_line = instance.read_line()
            while not (creature_line is None):
                print(f"\tLine from {id}: '{creature_line}'")
                creature_line = instance.read_line()

        instance.quit()
        print(f"Instance {id} quit.")

    thread1 = threading.Thread(target=create_and_run_instance, args=[1])
    thread2 = threading.Thread(target=create_and_run_instance, args=[2])
    thread3 = threading.Thread(target=create_and_run_instance, args=[3])

    thread1.start()
    thread2.start()
    thread3.start()

    thread1.join()
    thread2.join()
    thread3.join()
