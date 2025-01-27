import os
import sys

from langchain_openai import ChatOpenAI

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import asyncio

from browser_use import Agent, Browser, Controller


def get_code(input_str):
    if input_str.startswith("verify "):
        # Remove the prefix "verify " to get whatever follows
        code = input_str[len("verify ") :]

        # Optionally, verify the remainder is digits only
        if code.isdigit():
            print(f"The code is: {code}")
            return code
        else:
            print("No valid code found after 'verify '")
            return None
    else:
        print("Input does not start with 'verify '")
        return None


# Video: https://preview.screen.studio/share/8Elaq9sm
async def main():
    # Persist the browser state across agents

    browser = Browser()
    async with await browser.new_context() as context:
        model = ChatOpenAI(model="gpt-4o")
        current_agent = None

        async def get_input():
            return await asyncio.get_event_loop().run_in_executor(
                None,
                lambda: input(
                    "Enter task (p: pause current agent, r: resume, b: break): "
                ),
            )

        while True:
            task = await get_input()
            code = get_code(task)
            if task.lower() == "p":
                # Pause the current agent if one exists
                if current_agent:
                    current_agent.pause()
                continue
            elif task.lower() == "r":
                # Resume the current agent if one exists
                if current_agent:
                    current_agent.resume()
                continue
            elif task.lower() == "b":
                # Break the current agent's execution if one exists
                if current_agent:
                    current_agent.stop()
                    current_agent = None
                continue
            elif task.lower() == "login":
                if current_agent:
                    await current_agent.run()
                    current_agent.pause()
            elif task.lower() == "check_done":
                if current_agent:
                    current_agent.task = " if the url is https://www.linkedin.com/feed/ the login success then return TRUE of FALSE"
                    await current_agent.run()
                    current_agent.stop()
                    current_agent = None
                    await browser.close()

            elif code:
                if current_agent:
                    current_agent.resume()
                    current_agent.task = f"fill in code {code} to verfiy linkedin from previous task, if the url is https://www.linkedin.com/feed/ login success, if there is error login is failed"
                    await current_agent.run()
            # If there's a current agent running, pause it before starting new one
            if current_agent:
                current_agent.pause()

            # Create and run new agent with the task
            print("aaaaaaaaaaaaaaaaaaaaaaaaa", task)
            if task == "login":
                task = "i need login in linkedin.com, if it ask for verification code, return need verification code, if ask for captchar, return captchar need. linkedin pass is: , linkedni email is: "
            current_agent = Agent(
                task=task,
                llm=model,
                browser_context=context,
            )

            # Run the agent asynchronously without blocking
            asyncio.create_task(current_agent.run())


asyncio.run(main())

# Now aad the cheapest to the cart
