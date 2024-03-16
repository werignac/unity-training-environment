import threading
from enum import Enum
import win32file, win32pipe, win32event, pywintypes
import subprocess
import warnings


class SimulationTaskType(Enum):
    IDLE = 0
    SIMULATING = 1
    SETTING = 2
    QUIT = -1


# These keep track of the current state of the simulator and read messages back.
# also writes messages to pipe.
class SimulationTask:
    def __init__(self, pipe_handle, overlap):
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

    def get_task_type(self):
        return NotImplemented

    '''
    Called internally to see whether we've reached an 'end state'
    for this task.
    
    Override in conjunction with _on_read_line_from_pipe.
    '''
    def _get_finished_pipe_reading(self):
        return NotImplemented

    '''
    Called on reading thread when a full line with "\r\n" is finished being read
    from the pipe. 
    Returns whether the line should be added to the buffer.
    
    Usually overriden to include detecting when this task has been complete.
    '''
    def _on_read_line_from_pipe(self, line: str):
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

    '''
    Static function for seeing if a line is complete.
    '''
    def _is_line_finished(self, line):
        return line.endswith("\r\n")

    '''
    Function that runs on the read thread. Finishes when self._get_finished_pipe_reading()
    returns false.
    '''
    def _read_content(self):
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

    '''
    Returns a line written by the simulator. Blocks.
    Returns None if the simulator has reported that it has finished.
    '''
    def read_line(self, timeout=10.0, wait_step=0.5):
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
            if (wait_time > timeout) and (timeout >= 0):
                raise Exception(f"Timeout for {timeout} seconds when reading line.")

        return self.read_line()

    def write_line(self, to_write):
        # TODO: Check state of pipe
        ret, length = win32file.WriteFile(self.pipe_handle, f"{to_write}\n".encode())
        # TODO: Check ret and length

    def flush(self):
        # TODO: Check state of pipe
        win32file.FlushFileBuffers(self.pipe_handle)


class IdleTask(SimulationTask):
    def get_task_type(self):
        return SimulationTaskType.IDLE


class ExperimentTask(SimulationTask):
    def __init__(self, pipe_handle, overlap, experiment_name):
        SimulationTask.__init__(self, pipe_handle, overlap)
        self.has_sent_end = False
        self.has_received_end = False
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
    def __init__(self, pipe_handle, overlap):
        SimulationTask.__init__(self, pipe_handle, overlap)
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


class SimulationInstance:
    '''
    executable_args = {simulator_path, simulator_args}
    '''
    def __init__(self, pipe_path_and_name, executable_args=None):
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

        # Current (assumed) state of the simulator executable.
        self.task: SimulationTask = IdleTask(self.pipe_handle, self.overlap)

    def set_property(self, property_name, value):
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception("Cannot set properties whilst simulation is running.")

        # TODO: Write to pipe

    def run_experiment(self, experiment_name):
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception(f"Cannot start another task while task {self.task.get_task_type()} is running.")

        self.task = ExperimentTask(self.pipe_handle, self.overlap, experiment_name)

        self.task.signal_run_experiment()
        self.task.wait_run_experiment_response()

    def send_creatures(self, creatures):
        if self.task.get_task_type() != SimulationTaskType.SIMULATING:
            raise Exception("Cannot simulate creatures before starting an experiment.")

        if self.task.get_has_sent_end():
            raise Exception("Cannot send creatures. Already sent END for this simulation.")

        if type(creatures) == str or not hasattr(creatures, "__iter__"):
            creatures = [creatures]

        for creature in creatures:
            self.write_line(creature)

        self.flush_pipe()

    def end_send_creatures(self):
        if self.task.get_task_type() != SimulationTaskType.SIMULATING:
            raise Exception("Cannot end simulation. No simulations have started.")

        if self.task.get_has_sent_end():
            raise Exception("Cannot send end. Already sent end for this simulation.")

        self.write_line("END")
        self.flush_pipe()

    def quit(self):
        if self.task.get_task_type() != SimulationTaskType.IDLE:
            raise Exception("Cannot set quit whilst simulation is running.")

        self.task = QuitTask(self.pipe_handle, self.overlap)
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
            self.task = IdleTask(self.pipe_handle, self.overlap)

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

        instance = SimulationInstance('\\\\.\\pipe\\' + pipe_name, exec_args)

        for i in range(2):
            print(f"Running Experiment for {id}...")
            instance.run_experiment("falling_rectangular_prism")

            print(f"Sending Creatures for {id}...")

            for i in range(256):
                instance.send_creatures(json.dumps(main.RectPrism().serialize()))

            print(f"Sending End {id}...")
            instance.end_send_creatures()

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
