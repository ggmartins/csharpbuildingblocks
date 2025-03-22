import asyncio
import aiohttp
import time

class Result:
    def __init__(self, msg="", is_ok=False):
        self.msg = msg
        self.is_ok = is_ok

class Program:
    def __init__(self):
        self._executing = False

    @property
    def executing(self):
        return self._executing

    @executing.setter
    def executing(self, value):
        self._executing = value

    async def run1(self, msg, ct):
        print(f"INFO:run1 Executing...")
        await asyncio.sleep(10)
        result = Result(msg=msg, is_ok=True)
        print(f"INFO:run1 Done.")
        return result

    async def run2(self, msg, ct):
        print(f"INFO:run2 Executing...")
        await asyncio.sleep(10)
        result = Result(msg=msg, is_ok=True)
        print(f"INFO:run2 Done.")
        return result

    async def run3(self, msg, ct):
        print(f"INFO:run3 Executing...")
        await asyncio.sleep(0.1)
        result = Result(msg=msg, is_ok=True)
        print(f"INFO:run3 Done.")
        return result

    async def run4(self, msg, ct):
        print(f"INFO:run4 Executing...")
        await asyncio.sleep(0.1)
        print(f"Executing run4")
        raise Exception("run4 crashed")

    async def run5(self, msg, ct):
        print(f"INFO:run5 Executing...")
        async with aiohttp.ClientSession() as session:
            async with session.get('https://www.google.com') as response:
                print(f"RESPONSE: {response.status}")
                response.raise_for_status()
                content = await response.text()
                print(f"INFO:run5 Done.")
                return Result(msg=f"{msg}: Request succeeded with status {response.status}", is_ok=True)

    async def execute(self):
        self.executing = True
        tasks = [
            self.run1("test1", None),  # should timeout
            self.run2("test2", None),  # should timeout
            self.run3("test3", None),  # should execute
            self.run4("test4", None),  # should crash
            self.run5("test5", None),  # https get to google.com
        ]

        tasks_named = [(f"task{i+1}", task) for i, task in enumerate(tasks)]
        incomplete = []

        async def execute_task(name, task):
            """
            No, it doesn’t guarantee that. When you use asyncio.wait_for,
            if the timeout is reached the wrapped coroutine is cancelled,
            but that cancellation isn’t instantaneous or foolproof.
            If the coroutine catches the CancelledError or takes time to finish its cleanup,
            it might continue running in the background—potentially becoming an orphan task.
            In other words, while wait_for requests cancellation, you must ensure your coroutines
            handle cancellation properly to avoid leaving behind tasks that don’t terminate as expected.
            """
            try:
                completed_task = await asyncio.wait_for(task, timeout=2)
                return completed_task
            except asyncio.TimeoutError:
                incomplete.append((name, "Timeout", "Timeout"))
                return None
            except Exception as ex:
                incomplete.append((name, "Exception", str(ex)))
                pass #raise ex

        tasks_exec = [execute_task(name, task) for name, task in tasks_named]

        try:
            await asyncio.gather(*tasks_exec)
            #await asyncio.gather(*tasks)
        except Exception as ex:
            print(f"ERROR: {ex}")
        finally:
            self.executing = False

        print("WARN: INCOMPLETE TASKS:")
        for name, reason, error in incomplete:
            print(f"  * [{reason}: {name} ({error})]")

    @staticmethod
    def main():
        program = Program()
        asyncio.run(program.execute())


async def cancellable_sleep(delay, cancellation_event):
    """Sleeps for a specified delay or until cancelled."""
    sleep_task = asyncio.create_task(asyncio.sleep(delay))
    cancel_task = asyncio.create_task(cancellation_event.wait())

    done, pending = await asyncio.wait(
        [sleep_task, cancel_task],
        return_when=asyncio.FIRST_COMPLETED
    )

    for task in pending:
        task.cancel()

    if cancel_task in done:
        raise asyncio.CancelledError()

async def main():
    """Demonstrates cancellable sleep."""
    cancellation_event = asyncio.Event()

    # Example of cancelling the sleep after 2 seconds
    async def cancel_after_delay(delay):
        await asyncio.sleep(delay)
        cancellation_event.set()

    try:
        print("Starting sleep...")
        asyncio.create_task(cancel_after_delay(2))
        await cancellable_sleep(10, cancellation_event)
        print("Sleep finished.")
    except asyncio.CancelledError:
        print("Sleep cancelled.")

    # Reset the cancellation event for the next example
    cancellation_event.clear()

    try:
        print("Starting cancellable sleep with early cancellation...")
        asyncio.create_task(cancel_after_delay(2))
        await cancellable_sleep(10, cancellation_event)
        print("Sleep finished.")
    except asyncio.CancelledError:
        print("Sleep cancelled early.")

if __name__ == "__main__":
    asyncio.run(main())

if __name__ == "__main__":
    Program.main()
