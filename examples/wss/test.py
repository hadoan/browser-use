import asyncio

from dotenv import load_dotenv
from langchain_openai import ChatOpenAI
from browser_use.browser.browser import Browser, BrowserConfig
import os
from browser_use import Agent
from browser_use.browser.context import BrowserContextConfig
from browser_use import Agent, Controller
from browser_use.agent.views import ActionResult
from browser_use.browser.context import BrowserContext


load_dotenv()

# Initialize the model
llm = ChatOpenAI(
    model="gpt-4o",
    temperature=0.0,
)
controller = Controller()


async def wait_for_captcha(cdp_session):
    # Wait for the "Browserless.captchaFound" event
    future = asyncio.Future()

    def handle_captcha_found(event):
        print("Captcha found!")
        future.set_result(event)

    cdp_session.on("Browserless.captchaFound", handle_captcha_found)
    return await future


@controller.action("solve captcha", requires_browser=True)
async def solve_captcha(browser: BrowserContext):
    print("aaaaaaaaaaaaaaaa")
    page = await browser.get_current_page()

    # cdp = await page.context.new_cdp_session(page)

    # Navigate to the captcha demo page
    await page.goto(
        "https://www.google.com/recaptcha/api2/demo", wait_until="networkidle"
    )

    # Create a CDP session
    cdp = await page.context.new_cdp_session(page)

    # Wait for captcha to be found
    # await wait_for_captcha(cdp)

    # Solve the captcha
    result = await cdp.send("Browserless.solveCaptcha")
    solved, error = result.get("solved"), result.get("error")
    print({"solved": solved, "error": error})

    # result = await cdp.send("Browserless.solveCaptcha")
    # solved = result.get("solved")
    # error = result.get("error")

    print(
        {
            "solved": solved,
            "error": error,
        }
    )

    return ActionResult(is_done=solved, error=error)


wss_url = os.getenv("BROWSERLESS_WSS")
task = "Find the founders of tapline.io print the name of them, if it needs captcha, use solve captcha. If captcha solved continue finding founders, else FALSE"


context_config = BrowserContextConfig(
    cookies_file="cookies.json",
    wait_for_network_idle_page_load_time=3.0,
    browser_window_size={"width": 1280, "height": 1100},
    locale="en-US",
    user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36",
    highlight_elements=True,
    viewport_expansion=500,
    # allowed_domains=["google.com", "wikipedia.org"],
)


browser = Browser(
    config=BrowserConfig(wss_url=wss_url, new_context_config=context_config)
)


agent = Agent(
    task=task,
    llm=llm,
    browser=browser,
    controller=controller,
)


async def main():
    await agent.run()


if __name__ == "__main__":
    asyncio.run(main())
