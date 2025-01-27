import asyncio
import os
from dataclasses import dataclass
from typing import List, Optional

import gradio as gr
from dotenv import load_dotenv
from langchain_openai import ChatOpenAI
from rich.console import Console
from rich.panel import Panel
from rich.text import Text

from browser_use import Agent, Controller, Browser
from browser_use.browser.context import BrowserContext, BrowserContextConfig

load_dotenv()

controller = Controller()
verification_code: gr.Textbox | None = None


@dataclass
class ActionResult:
    is_done: bool
    extracted_content: Optional[str]
    error: Optional[str]
    include_in_memory: bool


@dataclass
class AgentHistoryList:
    all_results: List[ActionResult]
    all_model_outputs: List[dict]


def parse_agent_history(history_str: str) -> None:
    console = Console()

    # Split the content into sections based on ActionResult entries
    sections = history_str.split("ActionResult(")

    for i, section in enumerate(sections[1:], 1):  # Skip first empty section
        # Extract relevant information
        content = ""
        if "extracted_content=" in section:
            content = section.split("extracted_content=")[1].split(",")[0].strip("'")

        if content:
            header = Text(f"Step {i}", style="bold blue")
            panel = Panel(content, title=header, border_style="blue")
            console.print(panel)
            console.print()


# agent: Agent | None = None
# async def update_agent_memory():
# 	if agent:
# 		agent.pause()
# 		agent.llm.


@controller.registry.action("Verify with task ")
async def verify(code: str):
    try:
        task = f"fill verifiction code {code} to complete likedin login"
        agent = Agent(
            task=task,
            llm=ChatOpenAI(model="gpt-4o"),
        )
        result = await agent.run()
        #  TODO: The result cloud be parsed better
        return result
    except Exception as e:
        return f"Error: {str(e)}"


context: BrowserContext | None = None
browser: Browser | None = None


async def next_step(
    verification_code,
    model: str = "gpt-4o",
    headless: bool = True,
):
    # Pass the context to the next agent
    print("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
    print(verification_code)
    if verification_code:
        next_agent = Agent(
            task=f"fill verifiction code {verification_code} to complete likedin login and extract About",
            llm=ChatOpenAI(model="gpt-4o"),
            browser_context=context,
        )
        result = next_agent.run()
    else:
        next_agent = Agent(
            task=f"extract About from linkedin page ",
            llm=ChatOpenAI(model="gpt-4o"),
            browser_context=context,
        )
        result = next_agent.run()

    # Manually close the browser
    if context is not None:
        await context.close()

    if browser is not None:
        await browser.close()


@controller.action("Open website", requires_browser=True)
async def open_website(url: str, browser: Browser):
    page = browser.get_current_page()
    await page.goto(url)
    return ActionResult(extracted_content="Website opened")


async def run_browser_task(
    task: str,
    model: str = "gpt-4o",
    headless: bool = True,
):
    api_key = os.environ["OPENAI_API_KEY"]
    task = f"{task}"
    try:
        browser = Browser()
        context = await browser.new_context()

        agent = Agent(
            task=task,
            llm=ChatOpenAI(model="gpt-4o"),
        )
        result = await agent.run()

        return result
    except Exception as e:
        return f"Error: {str(e)}"


def create_ui():
    with gr.Blocks(title="Browser Use GUI") as interface:
        gr.Markdown("# Browser Use Task Automation")

        with gr.Row():
            with gr.Column():
                linkedin_email = gr.Textbox(
                    label="Email", placeholder="", type="password"
                )
                linkedin_pass = gr.Textbox(
                    label="Password", placeholder="", type="password"
                )
                verification_code = gr.Textbox(
                    label="Verification Code", placeholder="123456", type="text"
                )
                task = gr.Textbox(
                    label="Task Description",
                    placeholder="E.g., Find flights from New York to London for next week",
                    lines=3,
                )
                model = gr.Dropdown(
                    choices=["gpt-4", "gpt-3.5-turbo"], label="Model", value="gpt-4"
                )
                headless = gr.Checkbox(label="Run Headless", value=True)
                submit_btn = gr.Button("Run Task")
                add_verification_code_btn = gr.Button("Add Verification Code")

            with gr.Column():
                output = gr.Textbox(label="Output", lines=10, interactive=False)

        submit_btn.click(
            fn=lambda *args: asyncio.run(run_browser_task(*args)),
            inputs=[task, model, headless],
            outputs=output,
        )

        # add_verification_code_btn.click(
        #     fn=lambda *args: asyncio.run(next_step(*args)),
        #     inputs=[verification_code, model, headless],
        #     outputs=output,
        # )

    return interface


if __name__ == "__main__":
    demo = create_ui()
    demo.launch()
